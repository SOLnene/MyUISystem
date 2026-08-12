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
    [SerializeField]
    private TextAsset characterEnhanceTutorialConfig;
    private TutorialScheduler tutorialScheduler;
    
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
        mainMenuRedDotProvider.Bind(
            MainMenuRedDotKey.Backpack,
            GameContext.Instance.InventoryRepository.HasUnseenItems);
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
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F7))
        {
            RestartTutorialForTesting();
        }
#endif
    }

#if UNITY_EDITOR
    private void RestartTutorialForTesting()
    {
        // 每次测试都创建新的调度会话，避免复用已取消或已走完的步骤索引。
        tutorialScheduler?.Dispose();
        tutorialScheduler = new TutorialScheduler(
            new[] { characterEnhanceTutorialConfig },
            new CharacterEnhanceTutorialSignalAdapter());
        tutorialScheduler.Start();
    }
#endif

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
        tutorialScheduler?.Dispose();
        tutorialScheduler = null;
        mainMenuRedDotProvider?.Dispose();
        mainMenuRedDotProvider = null;
        base.OnRelease();
    }
}
