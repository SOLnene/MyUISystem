using System;

public class MainMenuViewModel
{
    public PlayerSummaryInfo PlayerSummary { get; }
    public IMainMenuRedDotProvider RedDotProvider { get; }

    private readonly Action<MainMenuAction> onActionRequested;

    public MainMenuViewModel(
        PlayerSummaryInfo playerSummary,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> onActionRequested)
    {
        PlayerSummary = playerSummary;
        RedDotProvider = redDotProvider;
        this.onActionRequested = onActionRequested;
    }

    public void RequestAction(MainMenuAction action)
    {
        onActionRequested?.Invoke(action);
    }
}
