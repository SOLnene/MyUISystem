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


    protected override UniTask PlayAnimation(bool isEnter,CancellationToken token)
    {
        seq?.Kill();
        seq = DOTween.Sequence()
            .Join(motionRoot.DOAnchorPos(originPos + targetMove, 0.35f)
                .From(originPos + originMove)
                .SetEase(Ease.OutCubic))
            .Join(motionGroup.DOFade(1, 0.25f)
                    .From(0.0f));
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
