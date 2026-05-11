using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SkierFramework;
using UnityEngine;
using UniTaskCompletionSource = Cysharp.Threading.Tasks.UniTaskCompletionSource;

public class SlideFadeMotion : UIMotionBase 
{

    [SerializeField]
    private CanvasGroup motionGroup;
    [SerializeField]
    private RectTransform motionRoot;
    Vector2 originPos;
    [SerializeField]
    Vector2 targetMove;
    [SerializeField]
    Vector2 originMove;
    [SerializeField]
    float moveDuration = 0.35f;
    [SerializeField]
    float fadeDuration = 0.25f;
    [SerializeField]
    Ease moveEase = Ease.OutCubic;
    [SerializeField]
    Ease fadeEase = Ease.Linear;

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
        var fromPos = isEnter ? originPos + originMove : originPos + targetMove;
        var toPos = isEnter ? originPos + targetMove : originPos + originMove;
        var fromAlpha = isEnter ? 0f : 1f;
        var toAlpha = isEnter ? 1f : 0f;

        seq?.Kill();
        seq = DOTween.Sequence()
            .Join(motionRoot.DOAnchorPos(toPos, moveDuration)
                .From(fromPos)
                .SetEase(moveEase))
            .Join(motionGroup.DOFade(toAlpha, fadeDuration)
                    .From(fromAlpha)
                    .SetEase(fadeEase));
        return seq.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
    }
    
    protected override void ApplyEndState(bool isEnter)
    {
        motionRoot.anchoredPosition = isEnter ? originPos + targetMove : originPos + originMove;
        motionGroup.alpha = isEnter ? 1 : 0;
    }
}
