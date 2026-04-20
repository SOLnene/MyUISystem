// FaceExpressionPreset.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FaceExpressionPreset", menuName = "Game/UI/ModelViewer/FaceExpressionPreset")]
public class FaceExpressionPreset : ScriptableObject
{
    [Serializable]
    public class BlendData
    {
        [Tooltip("BlendShape 名字（区分大小写）")]
        public string blendShapeName;

        [Range(0f, 1f)]
        [Tooltip("强度（0~1，对应 BlendShape 权重 0~100）")]
        public float weight = 1f;
    }

    public bool canBlink;
    
    [Tooltip("这个预设的表情名称（如 HappyLaugh）")]
    public string expressionName;

    [Tooltip("应用这个预设时要驱动的所有 BlendShape")]
    public List<BlendData> blends = new List<BlendData>();
}
