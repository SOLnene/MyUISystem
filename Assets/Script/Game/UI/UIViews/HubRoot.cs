using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public partial class HubRoot : UIView
{
    //UIControlData
    private bool mainMenuOpen;
    private MainMenuNavigator mainMenuNavigator;
    private MainMenuRedDotProvider mainMenuRedDotProvider;
    [SerializeField]
    private Button menuButton;
    [SerializeField]
    private TextAsset characterEnhanceTutorialConfig;
    private TutorialScheduler tutorialScheduler;
    [SerializeField] private RightHub rightHub;
    [SerializeField] private Material gameBackdropBlurMaterial;
    private HubTeamViewModel teamViewModel;
    private CancellationTokenSource mainMenuCaptureCancellation;
    
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
        ReleaseTeamDisplay();
        teamViewModel = new HubTeamViewModel(
            GameContext.Instance.TeamRepository, GameContext.Instance.CharacterRepository, FlowManager.Instance.WorldParty);
        rightHub.BindModel(teamViewModel);
        BottomHub.BindModel(teamViewModel);
        (data as StartupViewRequest)?.Complete(this);
    }

    /// <summary>
    /// 主界面测试
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    void Update()
    {
        if (!FlowManager.Instance.IsInWorld) return;
        if (FlowManager.Instance.IsPreview && Input.GetKeyDown(KeyCode.R))
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
        if (mainMenuOpen)
        {
            return;
        }

        mainMenuOpen = true;
        OpenMainMenuAfterCaptureAsync().Forget(Debug.LogException);
    }

    private async UniTask OpenMainMenuAfterCaptureAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        mainMenuCaptureCancellation = cancellation;
        try
        {
            // Capture the world before any page can hide its camera or cover it with UI.
            var world = FindObjectOfType<WorldSceneContext>();
            var camera = world != null ? world.WorldCamera : Camera.main;
            await UIBackdropCaptureService.Instance.CaptureGameAsync(
                camera, gameBackdropBlurMaterial, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            mainMenuOpen = false;
            return;
        }
        catch (Exception error)
        {
            // A missing capture must keep the menu usable; pages retain their prefab fallback.
            Debug.LogException(error, this);
        }
        finally
        {
            if (mainMenuCaptureCancellation == cancellation) mainMenuCaptureCancellation = null;
        }
        if (this == null || !isActiveAndEnabled || !FlowManager.Instance.IsInWorld || cancellation.IsCancellationRequested)
        {
            mainMenuOpen = false;
            return;
        }
        PlayerSummaryInfo playerSummary;
        var cloud = CloudGameSaveSession.Current;
        if (cloud != null && GameContext.Instance.ActiveSaveDirectory == cloud.Source)
            playerSummary = new PlayerSummaryInfo(cloud.Username, cloud.PlayerId);
        else
        {
            SaveProfileInfo profile = SaveProfileManager.Instance.ActiveProfile;
            // 在线账户接入前使用本地档案 ID 的前九位作为稳定展示 ID。
            playerSummary = new PlayerSummaryInfo(
                profile.displayName,
                profile.profileId.Substring(0, 9));
        }
        UIManager.Instance.Open(
            UIType.MainMenuView,
            new MainMenuOpenParams(
                playerSummary,
                mainMenuRedDotProvider,
                HandleMainMenuAction));
    }

    private void CloseMainMenu(Action callback = null)
    {
        if (mainMenuCaptureCancellation != null)
        {
            mainMenuCaptureCancellation.Cancel();
            return;
        }
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
        teamViewModel?.Refresh();
    }

    private void ReleaseTeamDisplay()
    {
        if (teamViewModel == null) return;
        rightHub.BindModel(null);
        BottomHub.BindModel(null);
        teamViewModel.Dispose();
        teamViewModel = null;
    }
    
    public override void OnAddListener()
    {
        base.OnAddListener();
        menuButton.onClick.AddListener(OpenMainMenu);
    }

    public override void OnRemoveListener()
    {
        menuButton.onClick.RemoveListener(OpenMainMenu);
        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        mainMenuCaptureCancellation?.Cancel();
        mainMenuOpen = false;
        ReleaseTeamDisplay();
        base.OnClose();
    }

    public override void OnRelease()
    {
        mainMenuCaptureCancellation?.Cancel();
        ReleaseTeamDisplay();
        tutorialScheduler?.Dispose();
        tutorialScheduler = null;
        mainMenuRedDotProvider?.Dispose();
        mainMenuRedDotProvider = null;
        base.OnRelease();
    }
}
