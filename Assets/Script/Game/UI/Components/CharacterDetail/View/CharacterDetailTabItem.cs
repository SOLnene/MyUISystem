using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailTabItem : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI text;
    [SerializeField]
    private Button btn;

    private static readonly string[] Labels =
    {
        "属性",
        "装备",
        "圣遗物",
        "详情"
    };

    private readonly Color selectedTextColor = new Color(1f, 0.96f, 0.88f, 1f);
    private readonly Color selectedBackgroundColor = new Color(0.95f, 0.86f, 0.64f, 0.18f);

    private Image background;
    private RectTransform textRectTransform;
    private Color normalTextColor;
    private Vector2 textBasePosition;
    private Vector3 textBaseScale;
    private Sequence selectionSequence;
    private bool cachedVisualState;
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
        SetSelected(cachedVisualState, true);
    }

    public void SetSelected(bool select)
    {
        SetSelected(select, false);
    }

    public void SetSelected(bool select, bool instant)
    {
        CacheVisualState();
        cachedVisualState = select;
        selectionSequence?.Kill();

        float targetBackgroundAlpha = select ? selectedBackgroundColor.a : 0f;
        Color targetTextColor = select ? selectedTextColor : normalTextColor;
        Vector2 targetTextPosition = select ? textBasePosition + new Vector2(14f, 0f) : textBasePosition;
        Vector3 targetTextScale = select ? textBaseScale * 1.08f : textBaseScale;

        if (instant)
        {
            background.color = new Color(selectedBackgroundColor.r, selectedBackgroundColor.g, selectedBackgroundColor.b, targetBackgroundAlpha);
            text.color = targetTextColor;
            textRectTransform.anchoredPosition = targetTextPosition;
            textRectTransform.localScale = targetTextScale;
            return;
        }

        selectionSequence = DOTween.Sequence();
        selectionSequence.Join(background.DOFade(targetBackgroundAlpha, 0.2f).SetEase(Ease.OutCubic));
        selectionSequence.Join(text.DOColor(targetTextColor, 0.18f).SetEase(Ease.OutCubic));
        selectionSequence.Join(textRectTransform.DOAnchorPos(targetTextPosition, 0.22f).SetEase(Ease.OutCubic));
        selectionSequence.Join(textRectTransform.DOScale(targetTextScale, 0.22f).SetEase(Ease.OutBack));
    }

    void CacheVisualState()
    {
        if (isCached)
        {
            return;
        }

        background = GetComponent<Image>();
        textRectTransform = text.rectTransform;
        normalTextColor = text.color;
        textBasePosition = textRectTransform.anchoredPosition;
        textBaseScale = textRectTransform.localScale;
        cachedVisualState = false;
        isCached = true;

        if (background != null)
        {
            background.color = new Color(selectedBackgroundColor.r, selectedBackgroundColor.g, selectedBackgroundColor.b, 0f);
        }
    }

    void OnDestroy()
    {
        selectionSequence?.Kill();
    }
}
