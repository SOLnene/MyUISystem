using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SkierFramework;
using UnityEngine;
using UniTaskCompletionSource = Cysharp.Threading.Tasks.UniTaskCompletionSource;

public enum SlideFadeMotionMode
{
    Manual,
    Preset
}

public class SlideFadeMotion : UIMotionBase 
{

    [SerializeField]
    private CanvasGroup motionGroup;
    [SerializeField]
    private RectTransform motionRoot;
    [SerializeField]
    SlideFadeMotionMode mode = SlideFadeMotionMode.Manual;
    [SerializeField]
    SlideFadeMotionPreset preset;
    Vector2 originPos;
    [SerializeField]
    Vector2 targetMove;
    [SerializeField]
    Vector2 originMove;
    [SerializeField]
    float moveDuration = 0.18f;
    [SerializeField]
    float fadeDuration = 0.12f;
    [SerializeField]
    Ease moveEase = Ease.OutCubic;
    [SerializeField]
    Ease fadeEase = Ease.Linear;

    internal override float TransitionDuration
    {
        get
        {
            if (mode == SlideFadeMotionMode.Preset)
            {
                return preset != null
                    ? Mathf.Max(preset.MoveDuration, preset.FadeDuration)
                    : 0f;
            }

            return Mathf.Max(moveDuration, fadeDuration);
        }
    }

    void Reset()
    {
        AutoBindReferences();
    }

    void AutoBindReferences()
    {
        if (motionGroup == null)
        {
            motionGroup = GetComponent<CanvasGroup>();
            if (motionGroup == null)
            {
                motionGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (motionRoot == null)
        {
            motionRoot = transform as RectTransform;
        }

        var animatedPanel = GetComponent<AnimatedPanel>();
        if (animatedPanel == null)
        {
            animatedPanel = gameObject.AddComponent<AnimatedPanel>();
        }

        animatedPanel.AutoBind(gameObject, this, motionGroup);
    }

    void Awake()
    {
        if (motionRoot != null)
        {
            originPos = motionRoot.anchoredPosition;
        }
    }

    protected override UniTask PlayAnimation(bool isEnter,CancellationToken token)
    {
        if (mode == SlideFadeMotionMode.Preset && preset == null)
        {
            Debug.LogError("SlideFadeMotion is set to Preset mode, but preset is missing.", this);
            return UniTask.CompletedTask;
        }

        var usePreset = mode == SlideFadeMotionMode.Preset;
        var activeTargetMove = usePreset ? preset.TargetMove : targetMove;
        var activeOriginMove = usePreset ? preset.OriginMove : originMove;
        var activeMoveDuration = usePreset ? preset.MoveDuration : moveDuration;
        var activeFadeDuration = usePreset ? preset.FadeDuration : fadeDuration;
        var activeMoveEase = usePreset ? preset.MoveEase : moveEase;
        var activeFadeEase = usePreset ? preset.FadeEase : fadeEase;
        var fromPos = isEnter ? originPos + activeOriginMove : originPos + activeTargetMove;
        var toPos = isEnter ? originPos + activeTargetMove : originPos + activeOriginMove;
        var fromAlpha = isEnter ? 0f : 1f;
        var toAlpha = isEnter ? 1f : 0f;

        seq?.Kill();
        seq = DOTween.Sequence()
            .Join(motionRoot.DOAnchorPos(toPos, activeMoveDuration)
                .From(fromPos)
                .SetEase(activeMoveEase))
            .Join(motionGroup.DOFade(toAlpha, activeFadeDuration)
                    .From(fromAlpha)
                    .SetEase(activeFadeEase));
        return seq.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
    }
    
    protected override void ApplyEndState(bool isEnter)
    {
        if (mode == SlideFadeMotionMode.Preset && preset == null)
        {
            Debug.LogError("SlideFadeMotion is set to Preset mode, but preset is missing.", this);
            return;
        }

        var usePreset = mode == SlideFadeMotionMode.Preset;
        var activeTargetMove = usePreset ? preset.TargetMove : targetMove;
        var activeOriginMove = usePreset ? preset.OriginMove : originMove;
        motionRoot.anchoredPosition = isEnter ? originPos + activeTargetMove : originPos + activeOriginMove;
        motionGroup.alpha = isEnter ? 1 : 0;
    }
}
