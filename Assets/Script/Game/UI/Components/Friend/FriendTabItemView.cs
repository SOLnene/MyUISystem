using UnityEngine;
using UnityEngine.UI;

public class FriendTabItemView : UITabItemView
{
    [SerializeField]
    Button button;
    [SerializeField]
    GameObject hoverState;
    [SerializeField]
    GameObject selectedState;

    void Awake()
    {
        button.onClick.AddListener(SelectSelf);
    }

    protected override void ApplyOption(UITabOption option)
    {
        // 好友页的固定 Tab 直接使用 Prefab 图标，这里只复用公共选择行为。
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        hoverState.SetActive(state == VisualState.Hover);
        selectedState.SetActive(state == VisualState.Selected);
    }

    void OnDestroy()
    {
        button.onClick.RemoveListener(SelectSelf);
    }
}
