using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

public class ModelViewer : SingletonMono<ModelViewer>
{
    enum CameraTransitionMotion
    {
        Linear,
        ConstantDeceleration
    }

    struct CameraPose
    {
        public Vector3 Position;
        public float Pitch;
        public float Yaw;
        public float FieldOfView;

        public static CameraPose Lerp(CameraPose start, CameraPose end, float progress)
        {
            return new CameraPose
            {
                Position = Vector3.Lerp(start.Position, end.Position, progress),
                Pitch = Mathf.Lerp(start.Pitch, end.Pitch, progress),
                Yaw = Mathf.Lerp(start.Yaw, end.Yaw, progress),
                FieldOfView = Mathf.Lerp(start.FieldOfView, end.FieldOfView, progress)
            };
        }
    }

    sealed class CameraTransition
    {
        public readonly CameraPose StartPose;
        public readonly CameraPose EndPose;
        public readonly float Duration;
        public readonly CameraTransitionMotion Motion;
        public readonly Action OnCompleted;
        public readonly UniTaskCompletionSource Completion = new();

        public float Elapsed;
        public bool WasCancelled;

        public CameraTransition(
            CameraPose startPose,
            CameraPose endPose,
            float duration,
            CameraTransitionMotion motion,
            Action onCompleted)
        {
            StartPose = startPose;
            EndPose = endPose;
            Duration = duration;
            Motion = motion;
            OnCompleted = onCompleted;
        }
    }

    sealed class CameraPushOperation
    {
        public readonly UniTaskCompletionSource Completion = new();
    }

    [Header("Transforms")]
    [SerializeField] Transform modelRoot;
    [SerializeField] ModelPreviewController previewController;
    //角色胸口
    [SerializeField] Transform cameraPivot;
    [FormerlySerializedAs("modelCamera")][SerializeField] Camera displayCamera;
    [SerializeField] Camera modelCamera;
    [SerializeField] Transform planeTransform;
    [SerializeField] Renderer planeRenderer;
    [SerializeField] PlanarReflectionManager reflectionManager;

    [Header("Equip Camera Handoff")]
    [SerializeField] float equipCameraDecelerationStartZ = 2.65f;
    [SerializeField, Min(0f)] float equipCameraDecelerationDuration = 0.16f;

    [Header("Background Effects")]
    [SerializeField] ParticleSystem starSphere;
    
    [Header("Rotation Settings")]
    [SerializeField] float rotateSensitivity = 0.4f;
    [SerializeField] float smoothSpeed = 10f; // 惯性速度，越大越灵敏
    [SerializeField] float minPitch = -15f;
    [SerializeField] float maxPitch = 25f;

    [Header("Zoom Settings")]
    [SerializeField] float zoomSensitivity = 2f;
    [SerializeField] float minDistance = 1.2f;
    [SerializeField] float maxDistance = 3.5f;
    [SerializeField] float heightLimit = 0.05f; // 相机最低高度，防止穿地面
    [SerializeField] float resetSpeed = 0.5f; //相机后移复位速度
    // 目标值（输入直接修改这些）
    CameraPose targetCameraPose;
    float moveAmount;

    // 记录初始的相机局部位置，用于 ResetView
    private Vector3 initialCameraLocalPos;
    Vector3 initialCameraWorldPos;
    // 当前值（用于插值平滑）
    CameraPose renderedCameraPose;
    
    // 是否在默认界面（可以手动控制视角）
    bool canDrag;
    //是否正在切换镜头/播放动画
    bool isInTransition;
    bool cameraTransitionPending;
    bool animationTransitionPending;
    bool faceTransitionPending;
    bool transitionAllowDrag;

    CameraTransition activeCameraTransition;
    CameraPushOperation cameraPushOperation;
    //todo:动态获取/更新
    int currentPresetIndex;
    //先这样测试
    [SerializeField]public  CameraPreset[] presets;
    [SerializeField] public FaceExpressionPreset[] facePresets;
    
    int starFieldUserCount;

    const string PlaneReflectionKeyword = "_PLANE_REFLECTION";

    public bool IsInTransition => isInTransition || cameraPushOperation != null;
    public event Action OnPreviewTransitionCompleted;
    void Start()
    {
        // 初始化，防止启动时猛烈旋转
        
        if (displayCamera)
        {
            initialCameraLocalPos = displayCamera.transform.localPosition;
            CameraPose initialPose = new()
            {
                Position = initialCameraLocalPos,
                Pitch = 0f,
                Yaw = 0f,
                FieldOfView = displayCamera.fieldOfView
            };
            targetCameraPose = initialPose;
            SetRenderedCameraPose(initialPose);
        }

        SetPlaneReflection(previewController.IsCharacterPreviewActive);
    }

    // 每一帧平滑处理
    void Update()
    {
    
        // 1. 平滑插值 (Lerp)
        targetCameraPose.Pitch = ClampPitchAbovePlane(
            targetCameraPose.Pitch,
            targetCameraPose.Yaw,
            targetCameraPose.Position);
        if (activeCameraTransition != null)
        {
            AdvanceCameraTransition(Time.deltaTime);
            return;
        }

        CameraPose nextPose = CameraPose.Lerp(
            renderedCameraPose,
            targetCameraPose,
            Time.deltaTime * smoothSpeed);
        nextPose.Pitch = ClampPitchAbovePlane(
            nextPose.Pitch,
            nextPose.Yaw,
            nextPose.Position);
        SetRenderedCameraPose(nextPose);
    }
    void LateUpdate()
    {
       // modelCamera.cullingMatrix = displayCamera.projectionMatrix * displayCamera.worldToCameraMatrix;
    }

    void ApplyTransforms()
    {
        // 相机父节点只绕 X 轴转（抬头/低头）
        if (cameraPivot)
            cameraPivot.localRotation = Quaternion.Euler(
                renderedCameraPose.Pitch,
                renderedCameraPose.Yaw,
                0);
    }

    // 由 PreviewDragController 调用
    public void Drag(Vector2 delta)
    {
        if (!canDrag)
        {
            return;
        }
        // 水平滑动 -> 修改模型旋转
        targetCameraPose.Yaw += delta.x * rotateSensitivity;
        
        // 垂直滑动 -> 修改相机俯仰
        float desiredPitch = Mathf.Clamp(
            targetCameraPose.Pitch + delta.y * rotateSensitivity,
            minPitch,
            maxPitch);
        targetCameraPose.Pitch = ClampPitchAbovePlane(
            desiredPitch,
            targetCameraPose.Yaw,
            targetCameraPose.Position);
    }

    public void Scroll(float scrollDelta, Vector2 viewportPos)
    {
        if (!canDrag)
        {
            return;
        }
        Vector3 targetDir;
        
        Ray ray = displayCamera.ViewportPointToRay(viewportPos);
        float step = scrollDelta * zoomSensitivity;

        if (scrollDelta > 0)
        {
            targetDir = ray.direction;
            Vector3 expectedWorldPos = displayCamera.transform.position + targetDir * step;
            //camerapivot在模型胸口处
            float distanceToModel =Math.Abs(expectedWorldPos.z - cameraPivot.transform.position.z) ;
            if (distanceToModel > maxDistance || distanceToModel < minDistance)
            {
                return;
            }
            Vector3 candidateTargetPos = displayCamera.transform.parent.InverseTransformPoint(expectedWorldPos);
            if (!IsCameraAbovePlane(
                    targetCameraPose.Pitch,
                    targetCameraPose.Yaw,
                    candidateTargetPos))
            {
                return;
            }
            targetCameraPose.Position = candidateTargetPos;
        }
        else
        {
            float t = Mathf.Abs(step) * resetSpeed ; // 0.8f 可调，控制复位速度
            Vector3 candidateTargetPos = Vector3.Lerp(
                targetCameraPose.Position,
                initialCameraLocalPos,
                t);
            if (!IsCameraAbovePlane(
                    targetCameraPose.Pitch,
                    targetCameraPose.Yaw,
                    candidateTargetPos))
            {
                return;
            }
            targetCameraPose.Position = candidateTargetPos;
            Debug.Log("After scroll: " + targetCameraPose.Position);
        }
    }

    private float ClampPitchAbovePlane(float desiredPitch, float yaw, Vector3 cameraLocalPosition)
    {
        desiredPitch = Mathf.Clamp(desiredPitch, minPitch, maxPitch);
        if (IsCameraAbovePlane(desiredPitch, yaw, cameraLocalPosition))
        {
            return desiredPitch;
        }

        float validPitch = minPitch;
        float invalidPitch = desiredPitch;
        for (int i = 0; i < 12; i++)
        {
            float middlePitch = (validPitch + invalidPitch) * 0.5f;
            if (IsCameraAbovePlane(middlePitch, yaw, cameraLocalPosition))
            {
                validPitch = middlePitch;
            }
            else
            {
                invalidPitch = middlePitch;
            }
        }

        return validPitch;
    }

    private bool IsCameraAbovePlane(float pitch, float yaw, Vector3 cameraLocalPosition)
    {
        Matrix4x4 pivotLocalToWorld = cameraPivot.parent.localToWorldMatrix * Matrix4x4.TRS(
            cameraPivot.localPosition,
            Quaternion.Euler(pitch, yaw, 0),
            cameraPivot.localScale);
        Vector3 cameraWorldPosition = pivotLocalToWorld.MultiplyPoint3x4(cameraLocalPosition);
        float distanceToPlane = Vector3.Dot(
            cameraWorldPosition - planeTransform.position,
            planeTransform.up.normalized);
        return distanceToPlane >= heightLimit;
    }

    // 防止相机穿过 plane
    private void ClampCameraAbovePlane()
    {
        if (displayCamera == null || planeTransform == null) return;
        
        Transform parentTransform = displayCamera.transform.parent;
        Vector3 predictedWorldPos = parentTransform.TransformPoint(targetCameraPose.Position);
        
        float camY =predictedWorldPos.y;
        float minY = planeTransform.position.y + heightLimit;

        if (camY >= minY) return;   // 没穿地，不用管

        // 当前从 Pivot 到相机的世界方向（单位向量）
        Vector3 pivotPos = cameraPivot.position;
        

        Vector3 camPos   = predictedWorldPos;
        Vector3 dirWorld = (camPos - pivotPos).normalized;
        float dirY = dirWorld.y;

        // 只有向下俯视时才需要限制（dirY < 0）
        if (dirY >= 0) return;

        // 公式：pivotY + dist * dirY >= minY  =>  dist <= (minY - pivotY) / dirY
        float maxAllowedDist = (minY - pivotPos.y) / dirY;
        maxAllowedDist = Mathf.Max(maxAllowedDist, minDistance); // 不能比最小距离还近

        float currentDist = targetCameraPose.Position.magnitude;
        if (currentDist > maxAllowedDist)
        {
            // 保持当前方向，强制缩短距离（这就是“缩小 localPosZ”的效果）
            Vector3 unitLocal = targetCameraPose.Position.normalized;
            targetCameraPose.Position = unitLocal * maxAllowedDist;
            Debug.Log("After Clamp: " + targetCameraPose.Position);
        }
    }
    
    public void SwitchPreview(int index, bool immediate = false)
    {
        if (isInTransition)
        {
            return;
        }

        if (presets == null || index < 0 || index >= presets.Length)
        {
            return;
        }

        CameraPreset preset = presets[index];
        FaceExpressionPreset facePreset = facePresets != null && index >= 0 && index < facePresets.Length
            ? facePresets[index]
            : null;

        currentPresetIndex = index;
        BeginTransition(preset.allowDrag);
        StartCameraTransition(preset, immediate);
        StartAnimationTransition(preset, immediate);
        StartFaceTransition(facePreset);
    }

    public void SwitchToPreset(CameraPreset preset,bool immediate = false)
    {
        if (isInTransition)
        {
            return;
        }

        BeginTransition(preset.allowDrag);
        StartCameraTransition(preset, immediate);
        StartAnimationTransition(preset, immediate);
        faceTransitionPending = false;
        TryCompleteTransition();
    }

    internal async UniTask PlayCameraTransitionAsync(
        CameraPreset preset,
        float startDelaySeconds,
        CancellationToken cancellationToken)
    {
        if (preset == null)
        {
            return;
        }

        CameraPushOperation activePush = cameraPushOperation;
        if (activePush != null)
        {
            await activePush.Completion.Task.AttachExternalCancellation(cancellationToken);
            return;
        }

        CameraPushOperation pushOperation = new();
        cameraPushOperation = pushOperation;
        bool previousCanDrag = canDrag;
        bool completed = false;
        CameraTransition transition = null;

        try
        {
            await UniTask.WaitWhile(
                () => isInTransition,
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (startDelaySeconds > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(startDelaySeconds),
                    cancellationToken: cancellationToken);
            }

            previousCanDrag = canDrag;
            canDrag = false;
            float duration = Mathf.Max(0f, preset.transitionDuration);

            CameraPose destination = renderedCameraPose;
            destination.Position.z = preset.cameraLocalPosition.z;
            transition = BeginCameraTransition(
                destination,
                duration,
                null);
            await transition.Completion.Task.AttachExternalCancellation(cancellationToken);
            if (transition.WasCancelled)
            {
                return;
            }

            canDrag = preset.allowDrag;
            completed = true;
        }
        finally
        {
            if (!completed && transition != null)
            {
                CancelCameraTransition(transition);
            }

            if (!completed)
            {
                canDrag = previousCanDrag;
            }

            if (cameraPushOperation == pushOperation)
            {
                cameraPushOperation = null;
                pushOperation.Completion.TrySetResult();
            }
        }
    }

    void BeginTransition(bool allowDrag)
    {
        isInTransition = true;
        transitionAllowDrag = allowDrag;
        canDrag = false;
        cameraTransitionPending = true;
        animationTransitionPending = true;
        faceTransitionPending = true;
    }

    void StartCameraTransition(CameraPreset preset, bool immediate)
    {
        // Kill旧动画
        CancelCameraTransition(activeCameraTransition);

        CameraPose destination = CreateCameraPose(preset);

        if (immediate)
        {
            targetCameraPose = destination;
            SetRenderedCameraPose(destination);
            MarkCameraTransitionComplete();
            return;
        }

        BeginCameraTransition(
            destination,
            Mathf.Max(0f, preset.transitionDuration),
            MarkCameraTransitionComplete);
    }

    CameraTransition BeginCameraTransition(
        CameraPose destination,
        float duration,
        Action onCompleted,
        CameraTransitionMotion motion = CameraTransitionMotion.Linear)
    {
        CancelCameraTransition(activeCameraTransition);
        destination.Pitch = ClampPitchAbovePlane(
            destination.Pitch,
            destination.Yaw,
            destination.Position);
        targetCameraPose = destination;

        CameraTransition transition = new(
            renderedCameraPose,
            destination,
            duration,
            motion,
            onCompleted);
        activeCameraTransition = transition;
        if (duration <= 0f)
        {
            CompleteCameraTransition(transition);
        }

        return transition;
    }

    void AdvanceCameraTransition(float deltaTime)
    {
        CameraTransition transition = activeCameraTransition;
        transition.Elapsed += deltaTime;
        float progress = Mathf.Clamp01(transition.Elapsed / transition.Duration);
        float poseProgress = transition.Motion == CameraTransitionMotion.ConstantDeceleration
            ? 1f - (1f - progress) * (1f - progress)
            : progress;
        CameraPose nextPose = CameraPose.Lerp(
            transition.StartPose,
            transition.EndPose,
            poseProgress);
        nextPose.Pitch = ClampPitchAbovePlane(
            nextPose.Pitch,
            nextPose.Yaw,
            nextPose.Position);
        SetRenderedCameraPose(nextPose);

        if (progress >= 1f)
        {
            CompleteCameraTransition(transition);
        }
    }

    void CompleteCameraTransition(CameraTransition transition)
    {
        if (activeCameraTransition != transition)
        {
            return;
        }

        SetRenderedCameraPose(transition.EndPose);
        activeCameraTransition = null;
        transition.OnCompleted?.Invoke();
        transition.Completion.TrySetResult();
    }

    void CancelCameraTransition(CameraTransition transition)
    {
        if (transition == null || activeCameraTransition != transition)
        {
            return;
        }

        activeCameraTransition = null;
        targetCameraPose = renderedCameraPose;
        transition.WasCancelled = true;
        transition.Completion.TrySetResult();
    }

    CameraPose CreateCameraPose(CameraPreset preset)
    {
        return new CameraPose
        {
            Position = preset.cameraLocalPosition,
            Pitch = preset.pitch,
            Yaw = preset.yaw,
            FieldOfView = preset.fov
        };
    }

    void SetRenderedCameraPose(CameraPose pose)
    {
        // 镜头姿态只在这里写入实际 Transform，避免交互、preset 和过渡动画互相覆盖。
        renderedCameraPose = pose;
        displayCamera.transform.localPosition = pose.Position;
        displayCamera.fieldOfView = pose.FieldOfView;
        ApplyTransforms();
    }

    void StartAnimationTransition(CameraPreset preset, bool immediate)
    {
        previewController.ApplyAnimationPreset(
            preset,
            immediate,
            MarkAnimationTransitionComplete);
    }
    
    public void SwitchFacePreset(FaceExpressionPreset preset)
    {
        previewController.ApplyFacePreset(preset);
    }

    void StartFaceTransition(FaceExpressionPreset preset)
    {
        SwitchFacePreset(preset);
        MarkFaceTransitionComplete();
    }

    void MarkCameraTransitionComplete()
    {
        cameraTransitionPending = false;
        TryCompleteTransition();
    }

    void MarkAnimationTransitionComplete()
    {
        animationTransitionPending = false;
        TryCompleteTransition();
    }

    void MarkFaceTransitionComplete()
    {
        faceTransitionPending = false;
        TryCompleteTransition();
    }

    void TryCompleteTransition()
    {
        if (cameraTransitionPending || animationTransitionPending || faceTransitionPending)
        {
            return;
        }

        isInTransition = false;
        canDrag = transitionAllowDrag;
        OnPreviewTransitionCompleted?.Invoke();
    }

    public UniTask ShowEquipPreviewAsync(string equipKey)
    {
        return ShowPreviewAsync(ModelPreviewType.Equip, equipKey);
    }

    internal void PrepareEquipPreview(string equipKey)
    {
        previewController.Preload(ModelPreviewType.Equip, equipKey);
    }

    internal async UniTask<bool> CommitPreparedEquipPreviewAsync(
        string equipKey,
        CancellationToken cancellationToken)
    {
        ModelPreviewDefinition definition =
            await previewController.EnsurePreloadedAsync(
                ModelPreviewType.Equip,
                equipKey,
                cancellationToken);
        if (definition == null)
        {
            return false;
        }

        // 武器 preset 必须在推镜停止写入后应用，保证它是最终镜头状态。
        CameraPushOperation pushOperation = cameraPushOperation;
        if (pushOperation != null)
        {
            await pushOperation.Completion.Task.AttachExternalCancellation(cancellationToken);
        }

        if (!previewController.TryActivatePreloaded(
                ModelPreviewType.Equip,
                equipKey,
                out _))
        {
            return false;
        }

        SetPlaneReflection(false);
        CancelCameraTransition(activeCameraTransition);
        CameraPose equipPose = CreateCameraPose(definition.CameraPreset);
        CameraPose decelerationStartPose = equipPose;
        decelerationStartPose.Position.z = equipCameraDecelerationStartZ;
        targetCameraPose = decelerationStartPose;
        SetRenderedCameraPose(decelerationStartPose);

        CameraTransition decelerationTransition = BeginCameraTransition(
            equipPose,
            equipCameraDecelerationDuration,
            null,
            CameraTransitionMotion.ConstantDeceleration);
        bool completed = false;
        try
        {
            await decelerationTransition.Completion.Task
                .AttachExternalCancellation(cancellationToken);
            completed = !decelerationTransition.WasCancelled;
            if (completed)
            {
                canDrag = definition.CameraPreset.allowDrag;
            }

            return completed;
        }
        finally
        {
            if (!completed)
            {
                CancelCameraTransition(decelerationTransition);
            }
        }
    }

    public async UniTask ShowPreviewAsync(ModelPreviewType previewType, string targetKey)
    {
        await TryShowPreviewAsync(previewType, targetKey);
    }

    internal async UniTask<bool> TryShowPreviewAsync(ModelPreviewType previewType, string targetKey)
    {
        ModelPreviewDefinition definition =
            await previewController.ShowAsync(previewType, targetKey);
        if (definition == null)
        {
            return false;
        }

        SetPlaneReflection(previewType == ModelPreviewType.Character);
        ApplyPreviewCamera(definition.CameraPreset);
        if (previewType == ModelPreviewType.Character)
        {
            ApplyCurrentCharacterPresetImmediate();
        }

        return true;
    }

    internal void CancelPendingPreviewLoad()
    {
        previewController.CancelPendingLoad();
    }

    public void ShowCharacterPreview()
    {
        previewController.ShowDefaultCharacter();
        SetPlaneReflection(true);

        if (presets != null && presets.Length > 0)
        {
            ApplyPreviewCamera(presets[0]);
        }
        else
        {
            ResetView();
            canDrag = true;
        }
    }

    void ApplyPreviewCamera(CameraPreset preset)
    {
        if (preset == null)
        {
            ResetView();
            canDrag = true;
            return;
        }

        CancelCameraTransition(activeCameraTransition);
        CameraPose pose = CreateCameraPose(preset);
        targetCameraPose = pose;
        SetRenderedCameraPose(pose);
        isInTransition = false;
        cameraTransitionPending = false;
        animationTransitionPending = false;
        faceTransitionPending = false;
        canDrag = preset.allowDrag;
    }

    void ApplyCurrentCharacterPresetImmediate()
    {
        if (presets == null
            || currentPresetIndex < 0
            || currentPresetIndex >= presets.Length)
        {
            return;
        }

        CameraPreset preset = presets[currentPresetIndex];
        ApplyPreviewCamera(preset);
        previewController.ApplyAnimationPreset(preset, true, null);

        FaceExpressionPreset facePreset =
            facePresets != null && currentPresetIndex < facePresets.Length
                ? facePresets[currentPresetIndex]
                : null;
        previewController.ApplyFacePreset(facePreset);
    }

    void SetPlaneReflection(bool enabled)
    {
        if (enabled)
        {
            planeRenderer.sharedMaterial.EnableKeyword(PlaneReflectionKeyword);
        }
        else
        {
            planeRenderer.sharedMaterial.DisableKeyword(PlaneReflectionKeyword);
        }

        reflectionManager.enabled = enabled;
    }
    
    public void ResetView()
    {
        targetCameraPose.Yaw = 0;
        targetCameraPose.Pitch = 0;
        // 直接回到最开始记录的本地坐标
        targetCameraPose.Position = initialCameraLocalPos;
    }

    internal void PlayStarFieldParticles()
    {
        starFieldUserCount++;
        if (starFieldUserCount > 1)
        {
            return;
        }

        starSphere.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        starSphere.Play(false);
    }

    internal void StopStarFieldParticles()
    {
        starFieldUserCount = Mathf.Max(0, starFieldUserCount - 1);
        if (starFieldUserCount > 0)
        {
            return;
        }

        starSphere.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
