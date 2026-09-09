using System;

internal sealed class MainMenuOpenParams
{
    public PlayerSummaryInfo PlayerSummary { get; }
    public IMainMenuRedDotProvider RedDotProvider { get; }
    public Action<MainMenuAction> OnActionRequested { get; }

    public MainMenuOpenParams(
        PlayerSummaryInfo playerSummary,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> onActionRequested)
    {
        PlayerSummary = playerSummary;
        RedDotProvider = redDotProvider;
        OnActionRequested = onActionRequested;
    }
}
