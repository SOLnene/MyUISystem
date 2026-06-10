using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class CategoryButtonView : UIThreeStateSelectable
{
    [SerializeField]
    Button btn;
    [SerializeField]
    Image bgImage;
    [SerializeField]
    Image iconImage;

    public string CategoryName { get; private set; }

    public void Init(string categoryName, Action onClick)
    {
        Init(categoryName, null, onClick);
    }

    public void Init(string categoryName, Sprite iconSprite, Action onClick)
    {
        CategoryName = categoryName;
        if (iconSprite != null)
        {
            iconImage.sprite = iconSprite;
        }

        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke());
        SetSelected(IsSelected, true);
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        iconImage.transform.DOKill();
        bgImage.DOKill();
        iconImage.DOKill();

        bool selected = state == VisualState.Selected;
        bool hover = state == VisualState.Hover;
        Color iconColor = selected ? new Color(0.37f, 0.39f, 0.36f, 1f)
            : hover ? new Color(1f, 0.97f, 0.86f, 1f)
            : new Color(0.78f, 0.76f, 0.68f, 0.78f);
        float bgAlpha = selected ? 0.94f : 0f;
        Vector3 iconScale = Vector3.one * (selected ? 1.08f : hover ? 1.05f : 1f);

        if (instant || !stateChanged)
        {
            iconImage.color = iconColor;
            SetImageAlpha(bgImage, bgAlpha);
            iconImage.transform.localScale = iconScale;
            return;
        }

        iconImage.DOColor(iconColor, 0.14f).SetEase(Ease.OutCubic);
        bgImage.DOFade(bgAlpha, 0.16f).SetEase(Ease.OutCubic);
        iconImage.transform.DOScale(iconScale, 0.16f).SetEase(Ease.OutBack);
    }

    static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    void OnDestroy()
    {
        iconImage.transform.DOKill();
        bgImage.DOKill();
        iconImage.DOKill();
    }
}
