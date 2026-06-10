using System;
using System.Collections.Generic;

internal class MainMenuOpenParams
{
    public IReadOnlyList<MainMenuButtonData> Buttons { get; }
    public IMainMenuRedDotProvider RedDotProvider { get; }
    public Action<MainMenuAction> OnActionRequested { get; }

    public MainMenuOpenParams(
        Action<MainMenuAction> onActionRequested,
        IReadOnlyList<MainMenuButtonData> buttons = null,
        IMainMenuRedDotProvider redDotProvider = null)
    {
        OnActionRequested = onActionRequested;
        Buttons = buttons;
        RedDotProvider = redDotProvider;
    }
}
