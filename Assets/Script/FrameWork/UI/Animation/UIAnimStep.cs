using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
[System.Serializable]
public class UIAnimStep
{
    public string name;
    public enum SequenceType { Animation, Wait, SetActive, SFX, UnityEvent, LoadScene }
    public SequenceType Type;

    [Header("Common")]
    public float StartTime; // 插入时间点
    public float Duration;

    [Header("Target")]
    public GameObject Target;

    [Header("RectTransform")]
    public bool AnimateAnchoredPosition;
    public Vector3 AnchoredPositionStart;
    public Vector3 AnchoredPositionEnd;

    public bool AnimateLocalScale;
    public Vector3 LocalScaleStart;
    public Vector3 LocalScaleEnd;

    public bool AnimateRotation;
    public Vector3 RotationStart;
    public Vector3 RotationEnd;

    [Header("Image")]
    public bool AnimateColor;
    public Color ColorStart;
    public Color ColorEnd;

    public bool AnimateFillAmount;
    public float FillAmountStart;
    public float FillAmountEnd;

    [Header("CanvasGroup")]
    public bool AnimateAlpha;
    public float AlphaStart;
    public float AlphaEnd;

    [Header("TMP")]
    public bool AnimateTMPColor;
    public Color TMPColorStart;
    public Color TMPColorEnd;

    public bool AnimateMaxVisibleCharacters;
    public int MaxVisibleCharactersStart;
    public int MaxVisibleCharactersEnd;

    [Header("SetActive")]
    public bool IsActivating;

    [Header("SFX")]
    public AudioClip SFXFile;
    public int SFXIndex;

    [Header("UnityEvent")]
    public UnityEvent Event;

    [Header("Scene")]
    public string SceneToLoad;

    [Header("Ease")]
    public Ease EaseFunction = Ease.Linear;
}


[System.Serializable]
public class UIAnimGroup
{
    public string groupName;
    public bool isParallel = false; // true = 并行, false = 顺序
    public List<UIAnimStep> steps;
    //public List<UIAnimStepBase> stepss;
}
#region 可选使用策略模式 
//使用这种方法需要编写editor脚本来支持序列化 目前没有实现
/// <summary>
/// UI 动画步骤基类（策略模式）
// 每种类型继承它，实现 Init 和 Play
/// </summary>
public abstract class UIAnimStepBase
{
    public float StartTime;
    public float Duration;
    public abstract void Init();
    public abstract Tween Play();
}

/// <summary>
/// 缩放动画
/// </summary>
[Serializable]
public class ScaleStep : UIAnimStepBase
{
    public Transform Target;
    public Vector3 StartScale;
    public Vector3 EndScale;
    public override void Init() => Target.localScale = StartScale;
    public override Tween Play() => Target.DOScale(EndScale, Duration);
}

/// <summary>
/// 位置动画
/// </summary>
[Serializable]
public class PositionStep : UIAnimStepBase
{
    public Transform Target;
    public Vector3 StartPosition;
    public Vector3 EndPosition;
    public override void Init() => Target.localPosition = StartPosition;
    public override Tween Play() => Target.DOLocalMove(EndPosition, Duration);
}

/// <summary>
/// CanvasGroup Alpha 动画
/// </summary>
[Serializable]
public class AlphaStep : UIAnimStepBase
{
    public CanvasGroup Target;
    public float StartAlpha;
    public float EndAlpha;
    public override void Init() => Target.alpha = StartAlpha;
    public override Tween Play() => Target.DOFade(EndAlpha, Duration);
}

/// <summary>
/// TMP 文本渐显
/// </summary>
[Serializable]
public class TMPFadeStep : UIAnimStepBase
{
    public TMP_Text Target;
    public float StartAlpha;
    public float EndAlpha;
    public override void Init()
    {
        var c = Target.color;
        Target.color = new Color(c.r, c.g, c.b, StartAlpha);
    }
    public override Tween Play()
    {
        return Target.DOFade(EndAlpha, Duration);
    }
}

/// <summary>
/// Image 颜色动画
/// </summary>
[Serializable]
public class ImageColorStep : UIAnimStepBase
{
    public Image Target;
    public Color StartColor;
    public Color EndColor;
    public override void Init() => Target.color = StartColor;
    public override Tween Play() => Target.DOColor(EndColor, Duration);
}

/// <summary>
/// SetActive 操作
/// </summary>
[Serializable]
public class SetActiveStep : UIAnimStepBase
{
    public GameObject Target;
    public bool ActiveState;
    public override void Init() => Target.SetActive(!ActiveState); // 先反着，动画结束时设置正确
    public override Tween Play() => DOVirtual.DelayedCall(0, () => Target.SetActive(ActiveState));
}

/// <summary>
/// 播放声音
/// </summary>
[Serializable]
public class SFXStep : UIAnimStepBase
{
    public AudioClip Clip;
    public override void Init() { }
    public override Tween Play()
    {
        return DOVirtual.DelayedCall(0, () =>
        {
            if (Clip != null) AudioSource.PlayClipAtPoint(Clip, Vector3.zero);
        });
    }
}

/// <summary>
/// UnityEvent 执行
/// </summary>
[Serializable]
public class UnityEventStep : UIAnimStepBase
{
    public UnityEvent Event;
    public override void Init() { }
    public override Tween Play() => DOVirtual.DelayedCall(0, () => Event?.Invoke());
}

/// <summary>
/// 场景加载
/// </summary>
[Serializable]
public class LoadSceneStep : UIAnimStepBase
{
    public string SceneName;
    public override void Init() { }
    public override Tween Play() => DOVirtual.DelayedCall(0, () => SceneManager.LoadScene(SceneName));
}
#endregion