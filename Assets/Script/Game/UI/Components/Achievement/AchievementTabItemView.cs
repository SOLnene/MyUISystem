using System;
using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class AchievementTabItemView : UIThreeStateSelectable
{
    [SerializeField]
    Button button;
    [SerializeField]
    Image normalBg;
    [SerializeField]
    Image selectBg;
    [SerializeField]
    Image clickHighlight;
    [SerializeField]
    TextMeshProUGUI label;
    [SerializeField]
    TextMeshProUGUI progressText;
    [SerializeField]
    GameObject redDot;
    [SerializeField]
    RectTransform visualRoot;
    [SerializeField]
    AnimatedPanel anim;
    [SerializeField]
    float selectedScale = 1.05f;
    [SerializeField]
    float scaleDuration = 0.12f;

    const float ClickHighlightFadeDuration = 0.15f;
    static readonly Color SelectedTextColor = new(0.22f, 0.27f, 0.36f, 1f);

    readonly CompositeDisposable bindDisposables = new();
    Tween clickHighlightTween;

    string categoryId;
    Action<string> onSelected;

    public string CategoryId => categoryId;
    internal AnimatedPanel Anim => anim;

    public void Bind(AchievementCategoryTabViewModel viewModel, Action<string> selectHandler)
    {
        // 进度由分类 VM 的 RP 推送，Tab 只负责展示，不主动查询页面状态。
        bindDisposables.Clear();
        categoryId = viewModel.Id;
        onSelected = selectHandler;
        label.text = viewModel.Name;
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
        clickHighlightTween?.Kill();
        clickHighlight.gameObject.SetActive(false);
        viewModel.ProgressPercent
            .Subscribe(value => progressText.text = $"{value}%")
            .AddTo(bindDisposables);
        viewModel.HasClaimableReward
            .Subscribe(redDot.SetActive)
            .AddTo(bindDisposables);
        SetSelected(IsSelected, true);
    }

    public void Unbind()
    {
        bindDisposables.Clear();
        button.onClick.RemoveAllListeners();
        clickHighlightTween?.Kill();
        clickHighlight.gameObject.SetActive(false);
        redDot.SetActive(false);
        categoryId = null;
        onSelected = null;
        SetSelected(false, true);
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        normalBg.gameObject.SetActive(state != VisualState.Selected);
        selectBg.gameObject.SetActive(state == VisualState.Selected);
        Color textColor = state == VisualState.Selected
            ? SelectedTextColor
            : Color.white;
        label.color = textColor;
        progressText.color = textColor;

        // 缩放 VisualRoot 而不是布局根节点，避免影响 VerticalLayoutGroup 的布局槽。
        Vector3 targetScale = state == VisualState.Selected
            ? Vector3.one * selectedScale
            : Vector3.one;
        visualRoot.DOKill();
        if (instant)
        {
            visualRoot.localScale = targetScale;
            return;
        }

        visualRoot
            .DOScale(targetScale, scaleDuration)
            .SetEase(Ease.OutQuad);
    }

    void HandleClick()
    {
        PlayClickHighlight();
        onSelected?.Invoke(categoryId);
    }

    void PlayClickHighlight()
    {
        clickHighlightTween?.Kill();
        clickHighlight.gameObject.SetActive(true);

        Color color = clickHighlight.color;
        color.a = 1f;
        clickHighlight.color = color;

        clickHighlightTween = clickHighlight
            .DOFade(0f, ClickHighlightFadeDuration)
            .OnComplete(() => clickHighlight.gameObject.SetActive(false));
    }

    void OnDestroy()
    {
        bindDisposables.Dispose();
        clickHighlightTween?.Kill();
        visualRoot.DOKill();
        button.onClick.RemoveAllListeners();
    }
}
