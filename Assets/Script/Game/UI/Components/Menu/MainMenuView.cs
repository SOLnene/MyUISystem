using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum MainMenuAction
{
    None,
    Character,
    Weapon,
    Backpack,
    Gacha,
    Team,
    Map,
    Quest,
    Shop,
    Achievement,
    Friend,
    Settings,
}

public enum MainMenuRedDotKey
{
    None,
    Character,
    Weapon,
    Backpack,
    Gacha,
    Team,
    Map,
    Quest,
    Shop,
    Achievement,
}

[Serializable]
public struct MainMenuButtonData
{
    public string label;
    public Sprite icon;
    public bool interactable;
    public MainMenuAction action;
    public MainMenuRedDotKey redDotKey;
}

public class MainMenuView : UIView
{
    [SerializeField] private PlayerInfoView playerInfoView;
    [SerializeField] private MenuActionPanel actionPanel;
    [SerializeField] private List<MainMenuButtonData> defaultButtons = new List<MainMenuButtonData>();
    [SerializeField] private Button backButton;
    [SerializeField] private Button settingsButton;

    private MainMenuViewModel viewModel;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        var openParams = (MainMenuOpenParams)data;
        viewModel = new MainMenuViewModel(
            openParams.PlayerSummary,
            openParams.RedDotProvider,
            openParams.OnActionRequested);

        playerInfoView.Bind(viewModel.PlayerSummary);
        actionPanel.Bind(defaultButtons, viewModel.RedDotProvider, viewModel.RequestAction);
        // Reopening may reuse the same view instance; each sidebar action must remain bound once.
        backButton.onClick.RemoveListener(OnCancel);
        backButton.onClick.AddListener(OnCancel);
        settingsButton.onClick.RemoveListener(OpenSettings);
        settingsButton.onClick.AddListener(OpenSettings);
    }

    public override void OnClose()
    {
        backButton.onClick.RemoveListener(OnCancel);
        settingsButton.onClick.RemoveListener(OpenSettings);
        actionPanel.Clear();
        base.OnClose();
    }

    void OpenSettings()
    {
        viewModel.RequestAction(MainMenuAction.Settings);
    }

}
