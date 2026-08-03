using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreTabItemView : UITabItemView
{
    const float ClickHighlightFadeDuration = 0.15f;

    [SerializeField]
    Button button;
    [SerializeField]
    Image normalBg;
    [SerializeField]
    Image selectBg;
    [SerializeField]
    Image icon;
    [SerializeField]
    TextMeshProUGUI label;
    [SerializeField]
    Image clickHighlight;
    [SerializeField]
    RectTransform visualRoot;

    [SerializeField]
    Color normalBgColor = new Color(0.341f, 0.392f, 0.482f, 0.58f);
    [SerializeField]
    Color hoverBgColor = new Color(0.46f, 0.51f, 0.60f, 0.68f);
    [SerializeField]
    Color normalContentColor = new Color(0.92f, 0.89f, 0.82f, 1f);
    [SerializeField]
    Color selectedContentColor = new Color(0.22f, 0.27f, 0.36f, 1f);
    [SerializeField]
    float selectFadeInDuration = 0.16f;
    [SerializeField]
    float selectFadeOutDuration = 0.12f;
    [SerializeField]
    float selectedScale = 1.05f;
    [SerializeField]
    float scaleDuration = 0.12f;

    Tween selectBgTween;
    Tween clickHighlightTween;

    protected override void ApplyOption(UITabOption option)
    {
        label.text = option.Label;
        icon.gameObject.SetActive(option.Icon != null);
        if (option.Icon != null)
        {
            icon.sprite = option.Icon;
        }

        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
        clickHighlightTween?.Kill();
        clickHighlight.gameObject.SetActive(false);
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        bool selected = state == VisualState.Selected;
        bool hover = state == VisualState.Hover;
        Color contentColor = selected ? selectedContentColor : normalContentColor;

        SetSelectBgVisible(selected, instant || !stateChanged);
        normalBg.gameObject.SetActive(!selected);
        normalBg.color = hover ? hoverBgColor : normalBgColor;
        label.color = contentColor;
        icon.color = contentColor;

        Vector3 targetScale = selected ? Vector3.one * selectedScale : Vector3.one;
        visualRoot.DOKill();
        if (instant)
        {
            visualRoot.localScale = targetScale;
        }
        else
        {
            visualRoot
                .DOScale(targetScale, scaleDuration)
                .SetEase(Ease.OutQuad);
        }
    }

    void HandleClick()
    {
        clickHighlightTween?.Kill();
        clickHighlight.gameObject.SetActive(true);

        Color color = clickHighlight.color;
        color.a = 1f;
        clickHighlight.color = color;

        clickHighlightTween = clickHighlight
            .DOFade(0f, ClickHighlightFadeDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => clickHighlight.gameObject.SetActive(false));
        SelectSelf();
    }

    void SetSelectBgVisible(bool visible, bool instant)
    {
        selectBgTween?.Kill();

        if (instant)
        {
            selectBg.gameObject.SetActive(visible);
            SetImageAlpha(selectBg, visible ? 1f : 0f);
            return;
        }

        if (visible)
        {
            selectBg.gameObject.SetActive(true);
            SetImageAlpha(selectBg, 0f);
            selectBgTween = selectBg
                .DOFade(1f, selectFadeInDuration)
                .SetEase(Ease.OutCubic);
            return;
        }

        selectBgTween = selectBg
            .DOFade(0f, selectFadeOutDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => selectBg.gameObject.SetActive(false));
    }

    static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    void OnDestroy()
    {
        selectBgTween?.Kill();
        clickHighlightTween?.Kill();
        visualRoot.DOKill();
    }
}
