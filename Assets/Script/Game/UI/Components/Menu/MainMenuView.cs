using System;
using System.Collections.Generic;
using UnityEngine;

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
    }

    public override void OnClose()
    {
        actionPanel.Clear();
        base.OnClose();
    }

}
