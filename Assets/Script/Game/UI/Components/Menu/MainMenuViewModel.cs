using System;
using System.Collections.Generic;

public class MainMenuViewModel
{
    public IReadOnlyList<MainMenuButtonData> Buttons => buttons;
    public IMainMenuRedDotProvider RedDotProvider { get; }

    private readonly IReadOnlyList<MainMenuButtonData> buttons;
    private readonly Action<MainMenuAction> onActionRequested;

    public MainMenuViewModel(
        IReadOnlyList<MainMenuButtonData> buttons,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> onActionRequested = null)
    {
        this.buttons = buttons;
        RedDotProvider = redDotProvider;
        this.onActionRequested = onActionRequested;
    }

    public void RequestAction(MainMenuAction action)
    {
        onActionRequested?.Invoke(action);
    }
}
