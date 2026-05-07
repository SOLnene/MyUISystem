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


    void Awake()
    {
        if (motionRoot != null)
        {
            originPos = motionRoot.anchoredPosition;
        }
    }

    protected override UniTask PlayAnimation(bool isEnter,CancellationToken token)
    {
        seq?.Kill();
        seq = DOTween.Sequence()
            .Join(motionRoot.DOAnchorPos(originPos + targetMove, moveDuration)
                .From(originPos + originMove)
                .SetEase(moveEase))
            .Join(motionGroup.DOFade(1, fadeDuration)
                    .From(0.0f)
                    .SetEase(fadeEase));
        return seq.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
    }

    protected override void ApplyIdleState()
    {
        motionRoot.anchoredPosition = originPos+originMove;
        motionGroup.alpha = 0;
    }
    
    protected override void ApplyEndState(bool isEnter)
    {
        motionRoot.anchoredPosition = originPos+targetMove;
        motionGroup.alpha = 1;
    }
}
