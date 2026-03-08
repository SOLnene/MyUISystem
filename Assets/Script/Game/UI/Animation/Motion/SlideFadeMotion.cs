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
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private CanvasGroup motionGroup;
    [ControlBinding]
    private RectTransform motionRoot;

		#pragma warning restore 0649
#endregion

    Vector2 originPos;
    [SerializeField]
    Vector2 targetMove;
    [SerializeField]
    Vector2 originMove;
    protected override void AfterBind()
    {
        originPos = motionRoot.anchoredPosition;
    }

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
