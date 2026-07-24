using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class ModelViewer : SingletonMono<ModelViewer>
{
    [Header("Transforms")]
    [SerializeField] Transform modelRoot;
    [SerializeField] Transform characterRoot;
    [SerializeField] Transform equipRoot;
    [SerializeField] ModelPreviewDatabase modelPreviewDatabase;
    //角色胸口
    [SerializeField] Transform cameraPivot;
    [FormerlySerializedAs("modelCamera")][SerializeField] Camera displayCamera;
    [SerializeField] Camera modelCamera;
    [SerializeField] Transform planeTransform;
    [SerializeField] Renderer planeRenderer;
    [SerializeField] PlanarReflectionManager reflectionManager;

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
    float targetYaw;
    float targetPitch;
    float moveAmount;

    Vector3 targetPos;
    Vector3 currentPos;
    // 记录初始的相机局部位置，用于 ResetView
    private Vector3 initialCameraLocalPos;
    Vector3 initialCameraWorldPos;
    // 当前值（用于插值平滑）
    float currentYaw;
    float currentPitch;
    
    // 是否在默认界面（可以手动控制视角）
    bool canDrag;
    //是否正在切换镜头/播放动画
    bool isInTransition;
    bool cameraTransitionPending;
    bool animationTransitionPending;
    bool faceTransitionPending;
    bool transitionAllowDrag;

    Sequence seq;
    //todo:动态获取/更新
    [SerializeField] CharacterPreviewAnimator characterPreviewAnimator;
    //先这样测试
    [SerializeField]public  CameraPreset[] presets;
    [SerializeField] public FaceExpressionPreset[] facePresets;
    
    [SerializeField] FaceController faceController;

    CancellationTokenSource previewLoadCancellation;
    GameObject loadedPreviewObject;
    int previewRequestVersion;
    int starFieldUserCount;

    const string PlaneReflectionKeyword = "_PLANE_REFLECTION";

    public bool IsInTransition => isInTransition;
    public event Action OnPreviewTransitionCompleted;
    void Start()
    {
        // 初始化，防止启动时猛烈旋转
        currentYaw = targetYaw = 0;
        currentPitch = targetPitch = 0;
        
        if (displayCamera)
        {
            initialCameraLocalPos = displayCamera.transform.localPosition;
            currentPos = targetPos = initialCameraLocalPos;
        }

        SetPlaneReflection(characterRoot.gameObject.activeSelf);
    }

    // 每一帧平滑处理
    void Update()
    {
    
        // 1. 平滑插值 (Lerp)
        targetPitch = ClampPitchAbovePlane(targetPitch, targetYaw, targetPos);
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * smoothSpeed);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);
        currentPos = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * smoothSpeed);
        currentPitch = ClampPitchAbovePlane(currentPitch, currentYaw, currentPos);

        ApplyTransforms();
        // 实际相机控制 Z 轴偏移
        if (displayCamera)
        {
            displayCamera.transform.localPosition = currentPos;
        }
    }
    void LateUpdate()
    {
       // modelCamera.cullingMatrix = displayCamera.projectionMatrix * displayCamera.worldToCameraMatrix;
    }

    void ApplyTransforms()
    {
        // 相机父节点只绕 X 轴转（抬头/低头）
        if (cameraPivot)
            cameraPivot.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0);
    }

    // 由 PreviewDragController 调用
    public void Drag(Vector2 delta)
    {
        if (!canDrag)
        {
            return;
        }
        // 水平滑动 -> 修改模型旋转
        targetYaw += delta.x * rotateSensitivity; 
        
        // 垂直滑动 -> 修改相机俯仰
        float desiredPitch = Mathf.Clamp(targetPitch + delta.y * rotateSensitivity, minPitch, maxPitch);
        targetPitch = ClampPitchAbovePlane(desiredPitch, targetYaw, targetPos);
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
            if (!IsCameraAbovePlane(targetPitch, targetYaw, candidateTargetPos))
            {
                return;
            }
            targetPos = candidateTargetPos;
        }
        else
        {
            float t = Mathf.Abs(step) * resetSpeed ; // 0.8f 可调，控制复位速度
            Vector3 candidateTargetPos = Vector3.Lerp(targetPos, initialCameraLocalPos, t);
            if (!IsCameraAbovePlane(targetPitch, targetYaw, candidateTargetPos))
            {
                return;
            }
            targetPos = candidateTargetPos;
            Debug.Log("After scroll: " + targetPos);
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
        Vector3 predictedWorldPos = parentTransform.TransformPoint(targetPos);
        
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

        float currentDist = targetPos.magnitude;
        if (currentDist > maxAllowedDist)
        {
            // 保持当前方向，强制缩短距离（这就是“缩小 localPosZ”的效果）
            Vector3 unitLocal = targetPos.normalized;
            targetPos = unitLocal * maxAllowedDist;
            Debug.Log("After Clamp: " + targetPos);
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
        if (seq != null && seq.IsActive())
        {
            seq.Kill();
        }

        if (immediate)
        {
            targetPos = currentPos = preset.cameraLocalPosition;
            targetPitch = currentPitch = preset.pitch;
            targetYaw = currentYaw = preset.yaw;
            ApplyTransforms();

            if (displayCamera)
            {
                displayCamera.transform.localPosition = currentPos;
            }

            MarkCameraTransitionComplete();
            return;
        }

        seq = DOTween.Sequence();
        seq.Join(DOTween.To(() => targetPos, x => targetPos = x, preset.cameraLocalPosition, preset.transitionDuration));
        seq.Join(DOTween.To(() => targetPitch, x => targetPitch = x, preset.pitch, preset.transitionDuration));
        seq.Join(DOTween.To(() => targetYaw, y => targetYaw = y, preset.yaw, preset.transitionDuration));
        seq.OnComplete(() =>
        {
            MarkCameraTransitionComplete();
        });
    }

    void StartAnimationTransition(CameraPreset preset, bool immediate)
    {
        if (immediate)
        {
            characterPreviewAnimator.ApplyPresetImmediate(preset, MarkAnimationTransitionComplete);
            return;
        }

        characterPreviewAnimator.ApplyPreset(preset, MarkAnimationTransitionComplete);
    }
    
    public void SwitchFacePreset(FaceExpressionPreset preset)
    {
        faceController.ApplyFacePreset(preset);
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

    public async UniTask ShowPreviewAsync(ModelPreviewType previewType, string targetKey)
    {
        CancelPreviewLoad();
        ReleaseLoadedPreview();

        Transform previewRoot = previewType == ModelPreviewType.Character
            ? characterRoot
            : equipRoot;
        characterRoot.gameObject.SetActive(previewType == ModelPreviewType.Character);
        equipRoot.gameObject.SetActive(previewType == ModelPreviewType.Equip);
        SetDirectChildrenActive(previewRoot, false);
        SetPlaneReflection(previewType == ModelPreviewType.Character);

        ModelPreviewDefinition definition = modelPreviewDatabase.Get(previewType, targetKey);
        if (definition == null)
        {
            Debug.LogWarning($"Model preview definition not found: {previewType}/{targetKey}", this);
            return;
        }

        ApplyPreviewCamera(definition.CameraPreset);

        int requestVersion = previewRequestVersion;
        var cancellation = new CancellationTokenSource();
        previewLoadCancellation = cancellation;
        GameObject previewObject;
        try
        {
            previewObject = await ResourceManager.Instance.InstantiateItemAsync(
                definition.ModelAddress,
                previewRoot,
                false,
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (previewLoadCancellation == cancellation)
            {
                previewLoadCancellation.Dispose();
                previewLoadCancellation = null;
            }
        }

        if (previewObject == null)
        {
            return;
        }

        if (requestVersion != previewRequestVersion)
        {
            ResourceManager.Instance.Recycle(previewObject);
            return;
        }

        Transform previewTransform = previewObject.transform;
        previewTransform.localPosition = definition.LocalPosition;
        previewTransform.localRotation = Quaternion.Euler(definition.LocalEulerAngles);
        previewTransform.localScale = definition.LocalScale;
        SetLayerRecursively(previewObject, LayerMask.NameToLayer("ModelDisplay"));
        loadedPreviewObject = previewObject;
        previewObject.SetActive(true);
    }

    public void ShowCharacterPreview()
    {
        CancelPreviewLoad();
        ReleaseLoadedPreview();
        equipRoot.gameObject.SetActive(false);
        characterRoot.gameObject.SetActive(true);
        SetDirectChildrenActive(characterRoot, true);
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

        if (seq != null && seq.IsActive())
        {
            seq.Kill();
        }

        targetPos = currentPos = preset.cameraLocalPosition;
        targetPitch = currentPitch = preset.pitch;
        targetYaw = currentYaw = preset.yaw;
        displayCamera.transform.localPosition = currentPos;
        displayCamera.fieldOfView = preset.fov;
        ApplyTransforms();
        isInTransition = false;
        cameraTransitionPending = false;
        animationTransitionPending = false;
        faceTransitionPending = false;
        canDrag = preset.allowDrag;
    }

    void CancelPreviewLoad()
    {
        previewRequestVersion++;
        if (previewLoadCancellation == null)
        {
            return;
        }

        previewLoadCancellation.Cancel();
        previewLoadCancellation.Dispose();
        previewLoadCancellation = null;
    }

    void ReleaseLoadedPreview()
    {
        if (loadedPreviewObject == null)
        {
            return;
        }

        ResourceManager.Instance.Recycle(loadedPreviewObject);
        loadedPreviewObject = null;
    }

    static void SetDirectChildrenActive(Transform root, bool active)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            root.GetChild(i).gameObject.SetActive(active);
        }
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
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
        targetYaw = 0;
        targetPitch = 0;
        // 直接回到最开始记录的本地坐标
        targetPos = initialCameraLocalPos;
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
