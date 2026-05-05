using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DetailTabItem : UIThreeStateSelectable
{
    [SerializeField]
    private TextMeshProUGUI text;
    [SerializeField]
    private Button btn;
    [SerializeField]
    private Image hoverBg;
    [SerializeField]
    private Image diamondNormal;
    [SerializeField]
    private Image diamondSelected;
    [SerializeField]
    private Image arrow;
  

    private RectTransform textRectTransform;
    private Color normalTextColor;
    private Vector2 textBasePosition;
    private Vector3 textBaseScale;
    private float hoverBgBaseAlpha;
    private float diamondNormalBaseAlpha;
    private float diamondSelectedBaseAlpha;
    private Sequence stateSequence;
    private bool isCached;

    int index;

    public void Bind(int index,string label, Action onClick)
    {
        this.index = index;
        CacheVisualState();
        
        text.text = label;
        

        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke());
        SetSelected(IsSelected, true);
    }

    public void SetLabel(string label)
    {
        text.text = label;
    }

    void CacheVisualState()
    {
        if (isCached)
        {
            return;
        }

        textRectTransform = text.rectTransform;
        normalTextColor = new Color(text.color.r, text.color.g, text.color.b, 0.62f);
        textBasePosition = textRectTransform.anchoredPosition;
        textBaseScale = textRectTransform.localScale;
        hoverBgBaseAlpha = GetImageAlpha(hoverBg);
        diamondNormalBaseAlpha = GetImageAlpha(diamondNormal);
        diamondSelectedBaseAlpha = diamondSelected != null ? 1f : 0f;
        isCached = true;

        if (hoverBg != null)
        {
            SetImageAlpha(hoverBg, 0f);
        }

        SetImageAlpha(diamondSelected, 0f);
        SetArrowActive(false);
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        CacheVisualState();
        stateSequence?.Kill();

        bool isSelected = state == VisualState.Selected;
        bool isHover = state == VisualState.Hover;
        float targetBackgroundAlpha = isHover ? hoverBgBaseAlpha * 0.7f : 0f;
        Color targetTextColor = SetColorAlpha(normalTextColor, isSelected ? 1f : isHover ? 0.86f : normalTextColor.a);
        Vector2 targetTextPosition = textBasePosition + new Vector2(isSelected ? 14f : isHover ? 8f : 0f, 0f);
        Vector3 targetTextScale = textBaseScale * (isSelected ? 1.08f : isHover ? 1.03f : 1f);
        float targetNormalDiamondAlpha = isSelected ? 0f : diamondNormalBaseAlpha;
        float targetSelectedDiamondAlpha = isSelected || isHover ? diamondSelectedBaseAlpha : 0f;

        SetArrowActive(isSelected);

        if (instant || !stateChanged)
        {
            SetImageAlpha(hoverBg, targetBackgroundAlpha);
            SetImageAlpha(diamondNormal, targetNormalDiamondAlpha);
            SetImageAlpha(diamondSelected, targetSelectedDiamondAlpha);
            text.color = targetTextColor;
            textRectTransform.anchoredPosition = targetTextPosition;
            textRectTransform.localScale = targetTextScale;
            return;
        }

        stateSequence = DOTween.Sequence();

        if (hoverBg != null)
        {
            stateSequence.Join(hoverBg.DOFade(targetBackgroundAlpha, 0.2f).SetEase(Ease.OutCubic));
        }

        if (diamondNormal != null)
        {
            stateSequence.Join(diamondNormal.DOFade(targetNormalDiamondAlpha, 0.18f).SetEase(Ease.OutCubic));
        }

        if (diamondSelected != null)
        {
            stateSequence.Join(diamondSelected.DOFade(targetSelectedDiamondAlpha, 0.18f).SetEase(Ease.OutCubic));
        }

        stateSequence.Join(text.DOColor(targetTextColor, 0.18f).SetEase(Ease.OutCubic));
        stateSequence.Join(textRectTransform.DOAnchorPos(targetTextPosition, 0.22f).SetEase(Ease.OutCubic));
        stateSequence.Join(textRectTransform.DOScale(targetTextScale, 0.22f).SetEase(Ease.OutBack));
    }

    static float GetImageAlpha(Image image)
    {
        return image != null ? image.color.a : 0f;
    }

    static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    void SetArrowActive(bool active)
    {
        if (arrow == null || arrow.gameObject.activeSelf == active)
        {
            return;
        }

        arrow.gameObject.SetActive(active);
    }

    static Color SetColorAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    void OnDestroy()
    {
        stateSequence?.Kill();
    }
}
