using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreTabItemView : UITabItemView
{
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
    Color normalBgColor = new Color(0.341f, 0.392f, 0.482f, 0.58f);
    [SerializeField]
    Color hoverBgColor = new Color(0.46f, 0.51f, 0.60f, 0.68f);
    [SerializeField]
    Color normalContentColor = new Color(0.92f, 0.89f, 0.82f, 1f);
    [SerializeField]
    Color selectedContentColor = new Color(0.22f, 0.27f, 0.36f, 1f);

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
        button.onClick.AddListener(SelectSelf);
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        bool selected = state == VisualState.Selected;
        bool hover = state == VisualState.Hover;
        Color contentColor = selected ? selectedContentColor : normalContentColor;

        selectBg.gameObject.SetActive(selected);
        normalBg.gameObject.SetActive(!selected);
        normalBg.color = hover ? hoverBgColor : normalBgColor;
        label.color = contentColor;
        icon.color = contentColor;
    }
}
