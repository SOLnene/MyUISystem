using UnityEngine;
using UnityEngine.UI;

public sealed class TeamPresetTabItemView : UITabItemView
{
    [SerializeField] private Button button;
    [SerializeField] private Image normalVisual;
    [SerializeField] private Image hoverVisual;
    [SerializeField] private Image selectedVisual;

    protected override void ApplyOption(UITabOption option)
    {
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveListener(SelectSelf);
        button.onClick.AddListener(SelectSelf);
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        normalVisual.gameObject.SetActive(state == VisualState.Normal);
        hoverVisual.gameObject.SetActive(state == VisualState.Hover);
        selectedVisual.gameObject.SetActive(state == VisualState.Selected);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(SelectSelf);
    }
}
