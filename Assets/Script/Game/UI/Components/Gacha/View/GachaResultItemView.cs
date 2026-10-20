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







    [SerializeField] Image rarityBackground;
    [SerializeField] Image rarityBorder;
    [SerializeField] Image rarityGlow;
    [SerializeField] GachaResultItemFx resultFx;

    Sequence seq;
    GachaEntryViewModel vm;
    bool clickEnabled;
    public ReactiveCommand<GachaEntryViewModel> clickCommand;
    public void Bind(GachaEntryViewModel viewModel,ReactiveCommand<GachaEntryViewModel> command)
    {
        disposable.Clear();
        vm = viewModel;
        // Keep the crisp outline and rarity halo independent from the hover feedback.
        Color rarityColor = RarityConfig.GetColor(vm.Rarity - 1);
        // White restores the texture colors, including when a tinted card is reused.
        rarityBackground.color = Color.white;
        // A bright core stays legible while the softer halo carries the rarity hue.
        rarityBorder.color = Color.Lerp(rarityColor, Color.white, 0.55f);
        rarityGlow.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.5f);
        resultFx.BindRarity(vm.Rarity, rarityColor);
        viewModel.VisualLoaded.Subscribe(_ => ApplyVisual()).AddTo(disposable);
        ApplyVisual();
        clickCommand = command;
    }

    void ApplyVisual()
    {
        icon.sprite = vm.Icon;
        var rect = icon.rectTransform;
        if (vm.EntryType == GachaEntryType.Equip)
        {
            GachaWeaponImageLayout.Fit(icon, vm, ((RectTransform)rect.parent).rect.size,
                Vector2.zero, true);
        }
        else
        {
            // Reused slots must restore the character crop after displaying a weapon.
            icon.preserveAspect = false;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }

    public UniTask PlayEnter(CancellationToken token = default)
    {
        Debug.Log("View Play Enter");
        resultFx.Play();
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
        resultFx.StopAndClear();
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

    public void SetClickEnabled(bool enabled)
    {
        clickEnabled = enabled;
    }
    
    void ApplyFinalState()
    {
        seq?.Kill(true);
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one;
        contentGroup.alpha = 1;
        resultFx.Play();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!clickEnabled)
        {
            return;
        }

        contentFeedback?.OnHoverEnter();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!clickEnabled)
        {
            return;
        }

        contentFeedback?.OnHoverExit();
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!clickEnabled)
        {
            return;
        }

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
