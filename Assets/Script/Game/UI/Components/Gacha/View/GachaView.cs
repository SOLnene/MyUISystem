using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Newtonsoft.Json;

/*GachaViewModel	应用层状态机（Application State）
GachaService	抽卡规则 / 领域逻辑（Domain）
GachaPoolProvider	数据来源 / 配置读取（Infrastructure）
GachaSession	一次抽卡过程（Transient Domain State）*/
public partial class GachaView : UIView
{
    public GachaViewModel vm;
    CompositeDisposable disposable;
    GachaFlowController flowController;
    [SerializeField]
    GachaPoolUIConfigDatabase poolUIConfigDatabase;
    [SerializeField]
    Button closeBtn;
    [SerializeField]
    AnimatedPanel bottomHub;
    [SerializeField]
    GameObject inputBlocker;
    [SerializeField] UIRequestFeedback configFeedback;
    [SerializeField] GameObject configResultRoot;
    [SerializeField] TMP_Text configResultText;
    [SerializeField] Button configRetryButton;
    [SerializeField] GameObject configRefreshRoot;
    [SerializeField] TMP_Text configRefreshText;
    [SerializeField] Button configRefreshRetryButton;
    bool configurationVerified;
    string appliedConfigurationSignature;
    CancellationTokenSource configurationCancellation;
    GachaService boundGachaService;
    bool isClosing;
    bool isInputLocked;
    bool isOpening;
    int presentationVersion;
    GachaTopHubViewModel topVm;
    //UIControlData
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
        flowController = new GachaFlowController();
        Bind();
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        isClosing = false;
        isInputLocked = false;
        disposable.Clear();
        if (boundGachaService != GameContext.Instance.GachaService)
        {
            // 页面可能被 UIManager 缓存，换账号或本地档案后要绑定新会话。
            middleHub.CancelPresentation();
            vm.Dispose();
            appliedConfigurationSignature = null;
            boundGachaService = GameContext.Instance.GachaService;
            vm = new GachaViewModel(boundGachaService, GameContext.Instance.GachaVisualProvider, poolUIConfigDatabase);
            middleHub.Bind(vm);
        }
        vm.OnSessionStarted
            .Subscribe(flowController.StartSession)
            .AddTo(disposable);
        ShowHubs(++presentationVersion).Forget();
    }

    void Bind()
    {
        if (vm == null)
        {
            vm = new GachaViewModel(GameContext.Instance.GachaService,
                GameContext.Instance.GachaVisualProvider,
                poolUIConfigDatabase);
        }
        boundGachaService = GameContext.Instance.GachaService;
        disposable = new CompositeDisposable();
        configFeedback.HideProcessing();
        configResultRoot.SetActive(false);
        configRefreshRoot.SetActive(false);
        configRetryButton.onClick.AddListener(RetryConfiguration);
        configRefreshRetryButton.onClick.AddListener(RetryConfiguration);
        
        //TopHub
        topVm = new GachaTopHubViewModel(
            vm.PoolConfigs,
            vm.CurrentPoolConfig,
            vm.SwitchPool);
        // 加载时仍允许关闭，改为分别控制切池与抽卡按钮，不用全屏挡板。
        inputBlocker.SetActive(false);
        //middleView
        middleHub.InputLockChanged -= SetInputLocked;
        middleHub.InputLockChanged += SetInputLocked;
        middleHub.Bind(vm);
        
        Draw1Btn.onClick.RemoveAllListeners();
        Draw10Btn.onClick.RemoveAllListeners();
        Draw1Btn.onClick.AddListener(() =>
        {
            if (!isClosing && !isOpening && !isInputLocked && middleHub.IsReady && configurationVerified)
            {
                vm.drawCommand.Execute(1);
            }
        });
        Draw10Btn.onClick.AddListener(() =>
        {
            if (!isClosing && !isOpening && !isInputLocked && middleHub.IsReady && configurationVerified)
            {
                vm.drawCommand.Execute(10);
            }
        });
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(OnCancel);
    }

    async UniTask ShowHubs(int version)
    {
        configurationCancellation?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        configurationCancellation = request;
        isOpening = true;
        configurationVerified = false;
        configResultRoot.SetActive(false);
        configRefreshRoot.SetActive(false);
        bool showingCachedConfiguration = false;
        ApplyInputState();
        try
        {
            var cloud = CloudGameSaveSession.Current;
            bool cloudMode = CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory);
            if (cloudMode && (cloud == null || !cloud.IsReady || GameContext.Instance.ActiveSaveDirectory != cloud.Source))
                throw new InvalidOperationException("云端会话尚未就绪，请重新登录。");
            if (cloudMode)
            {
                var startup = GameStartupData.ForCloud(cloud);
                showingCachedConfiguration = startup.Gacha.TryGetCached(out var cached);
                if (showingCachedConfiguration)
                {
                    ApplyGachaSnapshot(cached);
                    ShowConfigRefreshStatus("正在确认卡池信息，可以浏览，确认后可抽卡。", false);
                    await PresentConfiguredPoolsAsync(request.Token);
                    request.Token.ThrowIfCancellationRequested();
                    if (version != presentationVersion || isClosing) return;
                    isOpening = false;
                    ApplyInputState();
                }
                else
                {
                    middleHub.CancelPresentation();
                    vm.ApplyPoolConfigs(Array.Empty<GachaPoolUIConfig>());
                    boundGachaService.ApplyRemoteConfiguration(new RemoteGachaConfiguration());
                    appliedConfigurationSignature = null;
                    RebuildPoolTabs();
                    configFeedback.ShowProcessing("正在获取当前卡池…", () => !request.IsCancellationRequested, OnCancel);
                    await UniTask.WhenAll(topHub.Bind(topVm), topHub.Show(), middleHub.Hide(), bottomHub.Hide())
                        .AttachExternalCancellation(request.Token);
                }
                var snapshot = !showingCachedConfiguration && startup.Gacha.TryGetCached(out var prefetched)
                    ? prefetched : await startup.Gacha.RefreshAsync(request.Token);
                request.Token.ThrowIfCancellationRequested();
                // 请求完成前关闭、重开或切换会话，旧配置都不能应用到新页面。
                if (version != presentationVersion || isClosing || cloud != CloudGameSaveSession.Current ||
                    boundGachaService != GameContext.Instance.GachaService)
                    return;
                bool changed = ApplyGachaSnapshot(snapshot);
                configurationVerified = true;
                configFeedback.HideProcessing();
                configRefreshRoot.SetActive(false);
                Debug.Log($"已获取当前卡池配置，版本: {snapshot.version}，开放数量: {vm.PoolConfigs.Count}");
                if (showingCachedConfiguration && !changed) return;
                isOpening = true;
                ApplyInputState();
            }
            else
            {
                boundGachaService.ApplyRemoteConfiguration(null);
                // Display assets are a registry; local availability comes from the local pool definitions.
                var localConfigs = new List<GachaPoolUIConfig>();
                foreach (var pool in GameDatabase.GachaPoolDatabase.AllPools)
                {
                    var config = poolUIConfigDatabase.GetConfig(pool.gachaKey);
                    if (config == null)
                        throw new InvalidDataException("Local pool display config is missing: " + pool.gachaKey);
                    localConfigs.Add(config);
                }
                vm.ApplyPoolConfigs(localConfigs);
                appliedConfigurationSignature = null;
                configurationVerified = true;
                RebuildPoolTabs();
            }
            configFeedback.HideProcessing();
            await PresentConfiguredPoolsAsync(request.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (version == presentationVersion && !isClosing && !request.IsCancellationRequested)
            {
                configurationVerified = false;
                if (showingCachedConfiguration)
                    ShowConfigRefreshStatus("卡池信息确认失败，可以浏览；请重试后再抽卡。", true);
                else
                {
                    vm.ApplyPoolConfigs(Array.Empty<GachaPoolUIConfig>());
                    ShowConfigUnavailable(exception is InvalidDataException
                        ? exception.Message : "卡池配置获取失败，请检查服务器后重试。");
                }
                Debug.LogWarning($"卡池配置未能应用: {exception.Message}", this);
            }
        }
        finally
        {
            // 关闭或重开会改变版本；旧的打开流程不得恢复新页面的输入。
            if (version == presentationVersion && !isClosing)
            {
                configFeedback.HideProcessing();
                isOpening = false;
                ApplyInputState();
            }
            if (ReferenceEquals(configurationCancellation, request))
                configurationCancellation = null;
        }
    }

    bool ApplyGachaSnapshot(GachaConfigSnapshot snapshot)
    {
        // Server time changes on every response; only configuration content should rebuild the visible page.
        string signature = snapshot.version + ":" + JsonConvert.SerializeObject(snapshot.pools);
        if (signature == appliedConfigurationSignature) return false;
        var selected = vm.CurrentPoolConfig.Value;
        var configuration = RemoteGachaConfiguration.Create(snapshot, poolUIConfigDatabase,
            GameDatabase.CharacterDatabase, GameDatabase.ItemDatabase);
        middleHub.CancelPresentation();
        boundGachaService.ApplyRemoteConfiguration(configuration);
        vm.ApplyPoolConfigs(configuration.UiConfigs);
        if (selected != null)
            foreach (var config in configuration.UiConfigs)
                if (config.gachaKey == selected.gachaKey) { vm.SwitchPool(config); break; }
        appliedConfigurationSignature = signature;
        RebuildPoolTabs();
        return true;
    }

    async UniTask PresentConfiguredPoolsAsync(CancellationToken cancellationToken)
    {
        if (vm.PoolConfigs.Count == 0)
        {
            ShowConfigUnavailable("当前没有开放的卡池。");
            await UniTask.WhenAll(topHub.Show(), topHub.Bind(topVm), middleHub.Hide(), bottomHub.Hide())
                .AttachExternalCancellation(cancellationToken);
            return;
        }
        // Tab feedback binds its controls on enable; activate the container before instantiating tabs on a reopened page.
        await UniTask.WhenAll(topHub.Show(), topHub.Bind(topVm), middleHub.Show(), bottomHub.Show())
            .AttachExternalCancellation(cancellationToken);
    }

    void ShowConfigRefreshStatus(string message, bool retryAvailable)
    {
        configRefreshText.text = message;
        configRefreshRetryButton.gameObject.SetActive(retryAvailable);
        configRefreshRoot.SetActive(true);
    }

    void RebuildPoolTabs()
    {
        topVm.Dispose();
        topVm = new GachaTopHubViewModel(vm.PoolConfigs, vm.CurrentPoolConfig, vm.SwitchPool);
    }

    void ShowConfigUnavailable(string message)
    {
        configResultText.text = message;
        configResultRoot.SetActive(true);
    }

    void RetryConfiguration()
    {
        if (!isClosing && !isOpening)
            ShowHubs(++presentationVersion).Forget();
    }

    async UniTask CloseWithAnimation()
    {
        int version = presentationVersion;
        ApplyInputState();
        try
        {
            await UniTask.WhenAll(topHub.Hide(), middleHub.Hide(), bottomHub.Hide());
            if (version == presentationVersion && isClosing)
            {
                base.OnCancel();
            }
        }
        finally
        {
            if (version == presentationVersion)
            {
                ApplyInputState();
            }
        }
    }

    void SetInputLocked(bool isLocked)
    {
        isInputLocked = isLocked;
        ApplyInputState();
    }

    void ApplyInputState()
    {
        bool canSwitch = !isClosing && !isOpening && !isInputLocked;
        topHub.SetPoolSwitchEnabled(canSwitch);
        // Transient draw locks are checked in click callbacks without changing button visuals.
        closeBtn.interactable = !isClosing;
    }

    public override void OnCancel()
    {
        if (isClosing)
        {
            return;
        }

        isClosing = true;
        configurationCancellation?.Cancel();
        ++presentationVersion;
        CloseWithAnimation().Forget();
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
        configurationCancellation?.Cancel();
        configFeedback.HideProcessing();
        configResultRoot.SetActive(false);
        configRefreshRoot.SetActive(false);
        configurationVerified = false;
        ++presentationVersion;
        isClosing = true;
        isOpening = false;
        middleHub.CancelPresentation();
        flowController.CancelSession();
        disposable.Clear();
        base.OnClose();
    }

    public override void OnRelease()
    {
        middleHub.InputLockChanged -= SetInputLocked;
        disposable.Dispose();
        topVm.Dispose();
        vm.Dispose();
        configurationCancellation?.Cancel();
        configRetryButton.onClick.RemoveListener(RetryConfiguration);
        configRefreshRetryButton.onClick.RemoveListener(RetryConfiguration);
        flowController.Dispose();
        base.OnRelease();
    }
}
