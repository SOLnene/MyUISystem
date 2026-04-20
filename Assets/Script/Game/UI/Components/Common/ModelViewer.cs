using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class ModelViewer : SingletonMono<ModelViewer>
{
    [Header("Transforms")]
    [SerializeField] Transform modelRoot;
    //角色胸口
    [SerializeField] Transform cameraPivot;
    [FormerlySerializedAs("modelCamera")][SerializeField] Camera displayCamera;
    [SerializeField] Camera modelCamera;
    [SerializeField] Transform planeTransform;
    
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

    Sequence seq;
    //todo:动态获取/更新
    [SerializeField] CharacterPreviewAnimator characterPreviewAnimator;
    //先这样测试
    [SerializeField]public  CameraPreset[] presets;
    [SerializeField] public FaceExpressionPreset[] facePresets;
    
    [SerializeField] FaceController faceController;
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
    }

    // 每一帧平滑处理
    void Update()
    {
    
        // 1. 平滑插值 (Lerp)
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * smoothSpeed);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);
        currentPos = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * smoothSpeed);
       
        ApplyTransforms();
        ClampCameraAbovePlane();
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
        targetPitch += delta.y * rotateSensitivity;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
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
            targetPos = displayCamera.transform.parent.InverseTransformPoint(expectedWorldPos);
        }
        else
        {
            float t = Mathf.Abs(step) * resetSpeed ; // 0.8f 可调，控制复位速度
            targetPos = Vector3.Lerp(targetPos, initialCameraLocalPos, t);
            Debug.Log("After scroll: " + targetPos);
        }
        ClampCameraAbovePlane();
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
    
    public void SwitchToPreset(CameraPreset preset,bool immediate = false)
    {
        canDrag = preset.allowDrag;
        if (isInTransition)
        {
            return;
        }
        isInTransition = true;
        // Kill旧动画
        if (seq != null && seq.IsActive())
            seq.Kill();
        
        seq = DOTween.Sequence();
        seq.Join(DOTween.To(() => targetPos, x => targetPos = x, preset.cameraLocalPosition, preset.transitionDuration));
        seq.Join(DOTween.To(() => targetPitch, x => targetPitch = x, preset.pitch, preset.transitionDuration));
        seq.Join(DOTween.To(() => targetYaw, y => targetYaw = y, preset.yaw, preset.transitionDuration));
        if (immediate)
        {
            targetPos = currentPos = preset.cameraLocalPosition;
            targetPitch = currentPitch = preset.pitch;
            targetYaw = currentYaw = preset.yaw;
            characterPreviewAnimator.ApplyPresetImmediate(preset);
            ApplyTransforms();

            if (displayCamera)
                displayCamera.transform.localPosition = currentPos;
            isInTransition = false;
        }
        else
        {
            characterPreviewAnimator.ApplyPreset(preset);
        }
       
        seq.OnComplete(() =>
        {
            isInTransition = false;
            canDrag = preset.allowDrag;
        });
    }
    
    public void SwitchFacePreset(FaceExpressionPreset preset)
    {
        faceController.ApplyFacePreset(preset);
    }
    
    public void ResetView()
    {
        targetYaw = 0;
        targetPitch = 0;
        // 直接回到最开始记录的本地坐标
        targetPos = initialCameraLocalPos;
    }
}
