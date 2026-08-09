using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;


public partial class HubRoot : UIView
{
    //UIControlData
    private bool mainMenuOpen;
    private MainMenuNavigator mainMenuNavigator;
    private MainMenuRedDotProvider mainMenuRedDotProvider;
    private CharacterEnhanceTutorialController characterEnhanceTutorialController;
    
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        mainMenuNavigator ??= new MainMenuNavigator();
        mainMenuRedDotProvider ??= new MainMenuRedDotProvider();
        mainMenuRedDotProvider.Bind(
            MainMenuRedDotKey.Achievement,
            GameContext.Instance.AchievementService.HasClaimableReward);
        characterEnhanceTutorialController ??= new CharacterEnhanceTutorialController();
        characterEnhanceTutorialController.TryStart();
    }

    /// <summary>
    /// 主界面测试
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            UpdatePlayerStats();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (mainMenuOpen)
            {
                CloseMainMenu();
            }
            else
            {
                OpenMainMenu();
            }
        }
    }

    private void OpenMainMenu()
    {
        mainMenuOpen = true;
        UIManager.Instance.Open(
            UIType.MainMenuView,
            new MainMenuOpenParams(
                HandleMainMenuAction,
                redDotProvider: mainMenuRedDotProvider));
    }

    private void CloseMainMenu(Action callback = null)
    {
        UIManager.Instance.Close(UIType.MainMenuView, () =>
        {
            mainMenuOpen = false;
            callback?.Invoke();
        });
    }

    private void HandleMainMenuAction(MainMenuAction action)
    {
        CloseMainMenu(() => mainMenuNavigator.Open(action));
    }

    public void UpdatePlayerStats()
    {
        int maxHp = Random.Range(0, 10000);
        PlayerStateEvent playerStateEvent = new PlayerStateEvent
        {
            characterId = Random.Range(0,4),
            maxHp = maxHp,
            hp = Random.Range(0,maxHp),
            level = Random.Range(1,100),
            charge = Random.Range(0,1)
        };
        EventBus<PlayerStateEvent>.Raise(playerStateEvent);
    }
    
    public override void OnAddListener()
    {
        base.OnAddListener();
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        base.OnClose();
    }

    public override void OnRelease()
    {
        characterEnhanceTutorialController?.Dispose();
        characterEnhanceTutorialController = null;
        mainMenuRedDotProvider?.Dispose();
        mainMenuRedDotProvider = null;
        base.OnRelease();
    }
}
