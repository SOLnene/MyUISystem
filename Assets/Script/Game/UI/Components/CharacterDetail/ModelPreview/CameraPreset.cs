using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CameraPreset",menuName = "Game/UI/ModelViewer/CameraPreset")]
public class CameraPreset : ScriptableObject
{
    public string key;
    [Header("是否允许手动拖拽视角")]
    public bool allowDrag;
    [Header("Camera Transform")]
    public Vector3 cameraLocalPosition; // 相机的局部坐标（也就是拉远的距离和偏移）
    public float pitch;                 // 仰俯角
    public float yaw;                   // 旋转角（如果有些特写需要特定角度侧脸）
    
    [Header("Camera Settings")]
    public float fov = 60f;             // 视野大小
    
    [Header("Animation")]
    public float transitionDuration = 0.5f; // 镜头过渡到这个状态需要的时间
    
    [Header("Animation")]
    public AnimationClip animationClip;   // 当前镜头对应的角色动画
    public float crossFadeDuration = 0.25f;
}
