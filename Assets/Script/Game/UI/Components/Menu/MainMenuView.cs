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
    [SerializeField] private MenuActionPanel actionPanel;
    [SerializeField] private List<MainMenuButtonData> defaultButtons = new List<MainMenuButtonData>();

    private MainMenuViewModel viewModel;
    private MainMenuRedDotProvider ownedRedDotProvider;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        viewModel = data as MainMenuViewModel;
        if (viewModel == null)
        {
            var openParams = data as MainMenuOpenParams;
            ownedRedDotProvider ??= new MainMenuRedDotProvider();
            viewModel = new MainMenuViewModel(
                openParams?.Buttons ?? defaultButtons,
                openParams?.RedDotProvider ?? ownedRedDotProvider,
                openParams?.OnActionRequested);
        }

        actionPanel.Bind(viewModel.Buttons, viewModel.RedDotProvider, viewModel.RequestAction);
    }

    public override void OnClose()
    {
        actionPanel.Clear();
        base.OnClose();
    }

    public override void OnRelease()
    {
        ownedRedDotProvider?.Dispose();
        ownedRedDotProvider = null;
        base.OnRelease();
    }
}
