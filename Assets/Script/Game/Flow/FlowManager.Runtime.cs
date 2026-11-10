using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using UnityEngine;
using UnityEngine.SceneManagement;

internal enum StartupState
{
    Booting, Login, PreparingSession, LoadingWorld, EnteringWorld, InWorld, EntryFailed, ReturningToLogin
}

public partial class FlowManager
{
    internal StartupState State { get; private set; } = StartupState.Booting;
    internal bool IsInWorld => State == StartupState.InWorld;
    internal bool IsPreview => string.IsNullOrEmpty(worldScenePath);
    internal Exception LastEntryError { get; private set; }

    Task initialization;
    bool uiConfigured, quitting;
    Scene startupScene, worldScene;
    Camera startupCamera;
    Light startupLight;
    AudioListener startupListener;
    string worldScenePath;
    LoginView loginView;
    UILoadingView loadingView;
    WorldSceneContext worldContext;
    ModelViewer modelViewer;
    CancellationTokenSource entryCancellation;

    internal CharacterModel WorldCharacter => worldContext != null ? worldContext.Character : null;
    internal WorldPartyController WorldParty => worldContext != null ? worldContext.Party : null;

    internal async UniTask BootAsync(Camera camera, Light light, string scenePath)
    {
        if (initialization == null)
        {
            startupCamera = camera;
            startupLight = light;
            startupListener = camera.GetComponent<AudioListener>();
            startupScene = camera.gameObject.scene;
            worldScenePath = scenePath;
            initialization = InitializeApplicationAsync().AsTask();
        }
        try { await initialization; }
        catch { initialization = null; throw; }
    }

    internal async UniTask BootPreviewAsync(Camera camera, string profileId, CancellationToken token)
    {
        if (initialization != null)
            throw new InvalidOperationException("A startup entry is already active.");
        startupCamera = camera;
        startupListener = camera.GetComponent<AudioListener>();
        startupScene = camera.gameObject.scene;
        worldScenePath = string.Empty;
        initialization = InitializePreviewAsync(profileId, token).AsTask();
        try { await initialization; }
        catch { initialization = null; throw; }
    }

    async UniTask InitializePreviewAsync(string profileId, CancellationToken token)
    {
        SetState(StartupState.Booting);
        await InitializeServicesAsync(token);
        // The authoring scene supplies its own environment; preview skips login, loading UI and world loading.
        if (!await PrepareLocalSessionAsync(profileId, token))
            throw new InvalidOperationException("UI preview profile could not be prepared.");
        token.ThrowIfCancellationRequested();
        await PrepareModelViewerAsync(token);
        PrepareWorldCharacter("lumine");
        await OpenStartupViewAsync<HubRoot>(UIType.HubRoot, token);
        SetState(StartupState.InWorld);
    }

    async UniTask PrepareModelViewerAsync(CancellationToken token)
    {
        if (modelViewer != null) return;
        // Authoring scenes already own a configured instance; never use the auto-creating singleton here.
        modelViewer = FindObjectOfType<ModelViewer>();
        if (modelViewer == null)
        {
            var instance = await ResourceManager.Instance.InstantiateItemAsync(
                "prefab/modelviewer", cancellationToken: token);
            if (instance == null) throw new InvalidOperationException("Model viewer could not be prepared.");
            modelViewer = instance.GetComponent<ModelViewer>();
        }
        modelViewer.SetPreviewRendering(false);
    }

    CharacterModel PrepareWorldCharacter(string characterKey)
    {
        var characters = GameContext.Instance.CharacterRepository;
        var character = characters.GetByKey(characterKey);
        // This map's fixed playable character is a starter grant, independent of opening any UI page.
        if (character == null)
        {
            var definition = GameDatabase.CharacterDatabase.Get(characterKey)
                ?? throw new InvalidOperationException("Starter character definition is missing: " + characterKey);
            character = characters.Add(definition);
            GameSaveCoordinator.Instance.MarkDirty();
        }
        // Preserve existing formations; only empty presets receive the starter.
        if (GameContext.Instance.TeamRepository.EnsureNonEmptyPresets("lumine"))
            GameSaveCoordinator.Instance.MarkDirty();
        return character;
    }

    async UniTask InitializeServicesAsync(CancellationToken token)
    {
        ResourceManager.Instance.Init();
        await ResourceManager.Instance.InitAsync();
        token.ThrowIfCancellationRequested();
        if (!uiConfigured)
        {
            await UIManager.Instance.InitUIConfig();
            uiConfigured = true;
        }
        token.ThrowIfCancellationRequested();
    }

    async UniTask InitializeApplicationAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        SetState(StartupState.Booting);
        await InitializeServicesAsync(token);
        // Preload assets, not active view instances: cached views may otherwise cover the login page.
        foreach (string address in new[] { "ui/popup/uiloadingview", "ui/view/loginview", "ui/popup/hubroot" })
            if (await ResourceManager.Instance.LoadAssetAsync<GameObject>(address, token) == null)
                throw new InvalidOperationException("Startup UI asset could not be loaded: " + address);
        SetState(StartupState.Login);
        loginView = await OpenStartupViewAsync<LoginView>(UIType.LoginView, token, true);
    }

    async UniTask<T> OpenStartupViewAsync<T>(UIType type, CancellationToken token, bool allowAutomaticLogin = false) where T : UIView
    {
        var request = new StartupViewRequest(allowAutomaticLogin);
        UIManager.Instance.Open(type, request);
        return (T)await request.Opened.AttachExternalCancellation(token);
    }

    internal UniTask<bool> EnterSelectedProfileAsync(string profileId, CancellationToken viewCancellation = default) =>
        RunEntryAsync(async token =>
        {
            if (!await PrepareLocalSessionAsync(profileId, token))
                throw new InvalidOperationException("本地存档准备未完成，请检查存档后重试。");
        }, viewCancellation);

    internal async UniTask EnterCloudGameAsync(string username, CancellationToken viewCancellation)
    {
        await RunEntryAsync(token => PrepareCloudSessionAsync(username, token), viewCancellation);
    }

    async UniTask<bool> RunEntryAsync(Func<CancellationToken, UniTask> prepare, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        if (State != StartupState.Login || entryCancellation != null) return false;
        using var entry = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        entryCancellation = entry;
        var token = entry.Token;
        var callerRegistration = caller.Register(CancelEntry);
        bool prepared = false;
        LastEntryError = null;
        try
        {
            SetState(StartupState.PreparingSession);
            loadingView = await OpenStartupViewAsync<UILoadingView>(UIType.UILoadingView, token);
            while (true)
            {
                try
                {
                    if (!prepared)
                    {
                        SetState(StartupState.PreparingSession);
                        loadingView.ShowProgress();
                        ReportProgress(.05f, "正在读取存档与队伍…");
                        await prepare(token);
                        prepared = true;
                    }
                    // Authentication/page lifetime stops owning the operation once world loading begins.
                    callerRegistration.Dispose();
                    token.ThrowIfCancellationRequested();
                    await EnterWorldAsync(token);
                    return true;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    LastEntryError = error;
                    Debug.LogWarning("Startup entry failed: " + error.Message);
                    await RestoreStartupWorldAsync();
                    SetState(StartupState.EntryFailed);
                    bool retry = await loadingView.WaitForRetryAsync(
                        prepared ? "地图加载未完成，请重试或返回登录。" : "游戏数据准备未完成，请重试或返回登录。", false, token);
                    if (!retry) CancelEntry();
                    token.ThrowIfCancellationRequested();
                    loadingView.ShowProgress();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!quitting && this != null) await RestoreStartupWorldAsync();
            return false;
        }
        finally
        {
            callerRegistration.Dispose();
            entryCancellation = null;
            if (!quitting && this != null && State != StartupState.InWorld)
            {
                if (loginView == null || !loginView.gameObject.activeSelf)
                    loginView = await OpenStartupViewAsync<LoginView>(UIType.LoginView, this.GetCancellationTokenOnDestroy());
                await CloseStartupLoadingAsync();
                SetState(StartupState.Login);
            }
        }
    }

    async UniTask EnterWorldAsync(CancellationToken token)
    {
        if (!IsPreview)
        {
            SetState(StartupState.LoadingWorld);
            if (!Application.CanStreamedLevelBeLoaded(worldScenePath))
                throw new InvalidOperationException("World scene is not registered in Build Settings: " + worldScenePath);
            if (SceneManager.GetSceneByPath(worldScenePath).isLoaded)
                throw new InvalidOperationException("World scene is already loaded outside the startup flow.");
            const float mapStart = 1f / 7f;
            const float mapReady = 3f / 7f;
            const string mapDescription = "正在加载地图…";
            ReportProgress(mapStart, mapDescription);
            // Let the loading view draw the new stage before starting a potentially blocking load.
            await UniTask.NextFrame(token);
            var operation = SceneManager.LoadSceneAsync(worldScenePath, LoadSceneMode.Additive);
            float reported = mapStart;
            while (!operation.isDone)
            {
                // Native scene loading cannot be cancelled. Finish it before cleanup/retry can own another scene.
                this.GetCancellationTokenOnDestroy().ThrowIfCancellationRequested();
                float progress = Mathf.Lerp(mapStart, mapReady, operation.progress / .9f);
                if (progress - reported > .02f) { ReportProgress(progress, mapDescription); reported = progress; }
                await UniTask.Yield();
            }
            worldScene = SceneManager.GetSceneByPath(worldScenePath);
            token.ThrowIfCancellationRequested();
            var contexts = worldScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldSceneContext>(true)).ToArray();
            if (contexts.Length != 1)
                throw new InvalidOperationException("World scene must contain exactly one WorldSceneContext.");
            worldContext = contexts[0];
            SceneManager.SetActiveScene(worldScene);
            startupCamera.enabled = false;
            if (startupLight != null) startupLight.enabled = false;
            if (startupListener != null) startupListener.enabled = false;
            ReportProgress(4f / 7f, "正在准备出战角色…");
            await UniTask.NextFrame(token);
            worldContext.BindCharacter(PrepareWorldCharacter(worldContext.CharacterKey));
            worldContext.Activate();
            await worldContext.Party.InitializeAsync(
                GameContext.Instance.TeamRepository, GameContext.Instance.CharacterRepository, token);
            Debug.Log("新场景加载完成: " + worldScene.name);
        }
        SetState(StartupState.EnteringWorld);
        ReportProgress(5f / 7f, "正在加载角色展示资源…");
        await UniTask.NextFrame(token);
        await PrepareModelViewerAsync(token);
        ReportProgress(6f / 7f, "正在准备游戏界面…");
        await UniTask.NextFrame(token);
        UIManager.Instance.Close(UIType.LoginView);
        await OpenStartupViewAsync<HubRoot>(UIType.HubRoot, token);
        await UniTask.WaitForEndOfFrame(this, token);
        ReportProgress(1f, "准备完成");
        await CloseStartupLoadingAsync();
        token.ThrowIfCancellationRequested();
        SetState(StartupState.InWorld);
    }

    internal void CancelEntry()
    {
        if (entryCancellation == null || entryCancellation.IsCancellationRequested) return;
        entryCancellation.Cancel();
        if (loginView != null) loginView.CancelEntry();
    }

    internal async UniTask ReturnToLoginAsync()
    {
        if (!IsInWorld || IsPreview) return;
        var token = this.GetCancellationTokenOnDestroy();
        SetState(StartupState.ReturningToLogin);
        try
        {
            loadingView = await OpenStartupViewAsync<UILoadingView>(UIType.UILoadingView, token);
            while (true)
            {
                try
                {
                    loadingView.ShowProgress(false);
                    ReportProgress(.1f, "正在保存进度…");
                    await GameSaveCoordinator.Instance.PrepareSessionSwitchAsync(token);
                    break;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error)
                {
                    LastEntryError = error;
                    Debug.LogWarning("Startup return save failed: " + error.Message);
                    if (!await loadingView.WaitForRetryAsync("当前进度尚未保存，请重试或返回游戏。", true, token))
                    {
                        await CloseStartupLoadingAsync();
                        SetState(StartupState.InWorld);
                        return;
                    }
                }
            }
            ReportProgress(.6f, "正在返回登录…");
            await RestoreStartupWorldAsync();
            GameStartupData.Clear();
            loginView = await OpenStartupViewAsync<LoginView>(UIType.LoginView, token);
            await CloseStartupLoadingAsync();
            SetState(StartupState.Login);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    async UniTask RestoreStartupWorldAsync()
    {
        if (quitting || this == null) return;
        // Close session-owned views before unloading their world and camera; cached prefab instances may remain.
        var views = FindObjectsOfType<UIView>(true)
            .Where(view => view.Handle != null && view.Handle.uiType != UIType.LoginView && view.Handle.uiType != UIType.UILoadingView)
            .OrderByDescending(view => view.Handle.order).ToArray();
        foreach (var view in views)
        {
            var handle = view.Handle;
            if (handle.uiState == UIState.Closed) continue;
            UIManager.Instance.Close(handle.uiType);
            await UniTask.WaitUntil(() => view == null || handle.uiState == UIState.Closed,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        if (modelViewer != null)
        {
            modelViewer.CancelPendingPreviewLoad();
            modelViewer.SetPreviewRendering(false);
        }
        if (worldContext != null) worldContext.Deactivate();
        worldContext = null;
        if (startupScene.IsValid() && startupScene.isLoaded) SceneManager.SetActiveScene(startupScene);
        startupCamera.enabled = true;
        if (startupLight != null) startupLight.enabled = true;
        if (startupListener != null) startupListener.enabled = true;
        if (worldScene.IsValid() && worldScene.isLoaded)
            await SceneManager.UnloadSceneAsync(worldScene);
        worldScene = default;
    }

    async UniTask CloseStartupLoadingAsync()
    {
        if (loadingView == null || !loadingView.gameObject.activeSelf) return;
        await loadingView.HideStartupAsync();
        if (quitting || this == null) return;
        UIManager.Instance.Close(UIType.UILoadingView);
    }

    void ReportProgress(float progress, string description) =>
        EventBus<LoadingProgressEvent>.Raise(new LoadingProgressEvent(progress, description));

    void SetState(StartupState state)
    {
        State = state;
#if UNITY_EDITOR
        Debug.Log("[Startup] " + state + " frame=" + Time.frameCount);
#endif
    }

    void OnApplicationQuit()
    {
        quitting = true;
        entryCancellation?.Cancel();
    }
}
