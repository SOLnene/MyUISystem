using System;

public abstract class UITabItemView : UIThreeStateSelectable
{
    Action<int> onClick;

    public int Index { get; private set; }
    public UITabOption Option { get; private set; }

    public void Bind(int index, UITabOption option, Action<int> clickHandler)
    {
        Index = index;
        Option = option;
        onClick = clickHandler;
        ApplyOption(option);
        SetSelected(IsSelected, true);
    }

    protected void SelectSelf()
    {
        onClick?.Invoke(Index);
    }

    protected abstract void ApplyOption(UITabOption option);
}
