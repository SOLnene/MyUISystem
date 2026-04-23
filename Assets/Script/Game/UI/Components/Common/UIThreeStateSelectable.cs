using UnityEngine;
using UnityEngine.EventSystems;

public abstract class UIThreeStateSelectable : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    protected enum VisualState
    {
        Normal,
        Hover,
        Selected
    }

    private bool isHovered;
    private bool isSelected;
    private bool hasStateApplied;
    private VisualState currentState;

    protected bool IsSelected => isSelected;
    protected bool IsHovered => isHovered;
    protected VisualState CurrentState => currentState;

    public void SetSelected(bool selected)
    {
        SetSelected(selected, false);
    }

    public void SetSelected(bool selected, bool instant)
    {
        isSelected = selected;
        RefreshState(instant);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshState(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshState(false);
    }

    protected void RefreshState(bool instant)
    {
        VisualState nextState = ResolveState();
        bool stateChanged = !hasStateApplied || currentState != nextState;
        currentState = nextState;
        hasStateApplied = true;
        ApplyVisualState(nextState, instant, stateChanged);
    }

    private VisualState ResolveState()
    {
        if (isSelected)
        {
            return VisualState.Selected;
        }

        if (isHovered)
        {
            return VisualState.Hover;
        }

        return VisualState.Normal;
    }

    protected abstract void ApplyVisualState(VisualState state, bool instant, bool stateChanged);
}
