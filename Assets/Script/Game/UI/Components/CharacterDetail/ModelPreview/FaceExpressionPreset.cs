// FaceExpressionPreset.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum FacePresetPlaybackMode
{
    StaticBlend,
    CurveAnimation
}

public enum FaceBlinkPolicy
{
    Automatic,
    Allow,
    Suppress
}

[Flags]
public enum FaceRegion
{
    None = 0,
    Mouth = 1,
    EyesAndBrows = 2
}

public enum FaceCurveBindingType
{
    Unresolved,
    BlendShape,
    Ignored
}

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

    [Serializable]
    public class CurveData
    {
        public int channelId;
        public int controllerIndex;
        public FaceCurveBindingType bindingType;
        public string blendShapeName;
        public AnimationCurve curve = new();
        public int preInfinity;
        public int postInfinity;
        public int rotationOrder;
    }

    public FaceBlinkPolicy blinkPolicy = FaceBlinkPolicy.Automatic;

    public FacePresetPlaybackMode playbackMode;

    public FaceRegion regions;

    public float duration;

    public bool containsBlink;
    
    [Tooltip("这个预设的表情名称（如 HappyLaugh）")]
    public string expressionName;

    [Tooltip("应用这个预设时要驱动的所有 BlendShape")]
    public List<BlendData> blends = new List<BlendData>();

    public List<CurveData> curves = new();
}
