using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailTabItem : UIThreeStateSelectable
{
    [SerializeField]
    private TextMeshProUGUI text;
    [SerializeField]
    private Button btn;
    [SerializeField]
    private Image hoverBg;

    private static readonly string[] Labels =
    {
        "属性",
        "装备",
        "圣遗物",
        "详情"
    };

    private readonly Color hoverTextColor = new Color(1f, 0.96f, 0.88f, 0.9f);
    private readonly Color selectedTextColor = new Color(1f, 0.96f, 0.88f, 1f);
    private readonly Color hoverBackgroundColor = new Color(0.95f, 0.86f, 0.64f, 0.1f);
    private readonly Color selectedBackgroundColor = new Color(0.95f, 0.86f, 0.64f, 0f);

    private RectTransform textRectTransform;
    private Color normalTextColor;
    private Vector2 textBasePosition;
    private Vector3 textBaseScale;
    private Sequence stateSequence;
    private bool isCached;

    int index;

    public void Bind(int index, Action onClick)
    {
        this.index = index;
        CacheVisualState();

        if (index >= 0 && index < Labels.Length)
        {
            text.text = Labels[index];
        }

        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke());
        SetSelected(IsSelected, true);
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
        isCached = true;

        if (hoverBg != null)
        {
            hoverBg.color = new Color(hoverBackgroundColor.r, hoverBackgroundColor.g, hoverBackgroundColor.b, 0f);
        }
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        CacheVisualState();
        stateSequence?.Kill();

        float targetBackgroundAlpha;
        Color targetTextColor;
        Vector2 targetTextPosition;
        Vector3 targetTextScale;

        switch (state)
        {
            case VisualState.Selected:
                targetBackgroundAlpha = selectedBackgroundColor.a;
                targetTextColor = selectedTextColor;
                targetTextPosition = textBasePosition + new Vector2(14f, 0f);
                targetTextScale = textBaseScale * 1.08f;
                break;
            case VisualState.Hover:
                targetBackgroundAlpha = hoverBackgroundColor.a;
                targetTextColor = hoverTextColor;
                targetTextPosition = textBasePosition + new Vector2(8f, 0f);
                targetTextScale = textBaseScale * 1.03f;
                break;
            default:
                targetBackgroundAlpha = 0f;
                targetTextColor = normalTextColor;
                targetTextPosition = textBasePosition;
                targetTextScale = textBaseScale;
                break;
        }

        if (instant || !stateChanged)
        {
            if (hoverBg != null)
            {
                hoverBg.color = new Color(hoverBackgroundColor.r, hoverBackgroundColor.g, hoverBackgroundColor.b, targetBackgroundAlpha);
            }

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

        stateSequence.Join(text.DOColor(targetTextColor, 0.18f).SetEase(Ease.OutCubic));
        stateSequence.Join(textRectTransform.DOAnchorPos(targetTextPosition, 0.22f).SetEase(Ease.OutCubic));
        stateSequence.Join(textRectTransform.DOScale(targetTextScale, 0.22f).SetEase(Ease.OutBack));
    }

    void OnDestroy()
    {
        stateSequence?.Kill();
    }
}
