using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using SkierFramework;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class GachaResultItemView : BindableUI
    ,IPointerEnterHandler
    ,IPointerExitHandler
    ,IPointerClickHandler
{
    CompositeDisposable disposable = new CompositeDisposable();

   #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private Image icon;
    [ControlBinding]
    private RectTransform content;
    [ControlBinding]
    private CanvasGroup contentGroup;
    [ControlBinding]
    private ItemGlowScaleFeedback contentFeedback;

		#pragma warning restore 0649
#endregion







    Sequence seq;
    GachaEntryViewModel vm;
    public ReactiveCommand<GachaEntryViewModel> clickCommand;
    public void Bind(GachaEntryViewModel viewModel,ReactiveCommand<GachaEntryViewModel> command)
    {
        disposable.Clear();
        vm = viewModel;
        icon.sprite = viewModel.Icon;
        clickCommand = command;
    }

    public UniTask PlayEnter(CancellationToken token = default)
    {
        Debug.Log("View Play Enter");
        seq?.Kill();
        seq = DOTween.Sequence()
            .Append(content.DOAnchorPosX(0f, 0.35f)
                .From(content.anchoredPosition + Vector2.right * 100f)
                .SetEase(Ease.OutCubic));
        seq.Join(content.DOScale(Vector3.one, 0.2f)
            .From(Vector3.one * 1.2f)
            .SetEase(Ease.OutCubic));
        seq.Join(contentGroup.DOFade(1.0f, 0.1f));

        return seq.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(token);
    }

    public void ResetForEnter()
    {
        seq?.Kill();
        Debug.Log("Reset Result Item");
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one;
        contentGroup.alpha = 0;
        contentFeedback?.Reset();
    }

    public void SkipToEnd()
    {
        ApplyFinalState();
    }
    
    void ApplyFinalState()
    {
        seq?.Kill(true);
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one;
        contentGroup.alpha = 1;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        contentFeedback?.OnHoverEnter();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        contentFeedback?.OnHoverExit();
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        contentFeedback?.OnSelect();
        clickCommand.Execute(vm);
    }
    
    public void OnDisable()
    {
        disposable.Clear();
    }

    public void OnDestroy()
    {
        disposable.Dispose();
    }
}
