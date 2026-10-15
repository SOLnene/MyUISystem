using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using TMPro;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public partial class LoginView : UIView
{
    [SerializeField]
    RectTransform saveListContent;

    [SerializeField]
    SaveProfileItemView saveItemTemplate;

    [SerializeField]
    Button createButton;

    [SerializeField]
    Button enterButton;

    [SerializeField]
    GameObject inputBlocker;

    [SerializeField] UIRequestFeedback requestFeedback;
    [SerializeField] TMP_Text loginResultText;
    [SerializeField] TMP_Text registrationResultText;
    [SerializeField] Toggle showPasswordToggle;
    [SerializeField] Toggle showRegistrationPasswordToggle;
    [SerializeField] Toggle showConfirmationPasswordToggle;
    [SerializeField] Toggle rememberLoginToggle;
    [SerializeField] Toggle autoLoginToggle;
    [SerializeField] Button localPlayButton;
    [SerializeField] Toggle autoStartServerToggle;

    [SerializeField]
    TMP_InputField usernameInput;

    [SerializeField]
    TMP_InputField passwordInput;

    [SerializeField]
    Button loginButton;

    [SerializeField]
    Button registerButton;

    [SerializeField]
    GameObject registrationPanel;

    [SerializeField]
    TMP_InputField registrationUsernameInput;

    [SerializeField]
    TMP_InputField registrationPasswordInput;

    [SerializeField]
    TMP_InputField registrationConfirmPasswordInput;

    [SerializeField]
    Button registrationSubmitButton;

    [SerializeField]
    Button registrationBackButton;

    [SerializeField]
    Button registrationCloseButton;

    // 通过档案 ID 定位旧、新选中项，切换时无需刷新整个列表。
    readonly Dictionary<string, SaveProfileItemView> profileItems = new();

    LoginViewModel viewModel;
    bool isEnteringProfile;
    int formRevision;
    CancellationTokenSource entryCancellation;

    void Reset()
    {
        saveListContent = transform.Find("PageRoot/SaveListScroll/Viewport/SaveListContent") as RectTransform;
        saveItemTemplate = saveListContent.GetComponentInChildren<SaveProfileItemView>(true);
        createButton = transform.Find("PageRoot/CreateButton").GetComponent<Button>();
        enterButton = transform.Find("PageRoot/EnterButton").GetComponent<Button>();
        inputBlocker = transform.Find("PageRoot/InputBlocker").gameObject;
        usernameInput = transform.Find("PageRoot/AccountPanel/UsernameInput").GetComponent<TMP_InputField>();
        passwordInput = transform.Find("PageRoot/AccountPanel/PasswordInput").GetComponent<TMP_InputField>();
        loginButton = transform.Find("PageRoot/AccountPanel/LoginButton").GetComponent<Button>();
        registerButton = transform.Find("PageRoot/AccountPanel/RegisterButton").GetComponent<Button>();
        registrationPanel = transform.Find("PageRoot/RegistrationOverlay").gameObject;
        Transform registrationContent = registrationPanel.transform.Find("ContentRoot");
        registrationUsernameInput = registrationContent.Find("Body/UsernameRow/UsernameInput").GetComponent<TMP_InputField>();
        registrationPasswordInput = registrationContent.Find("Body/PasswordRow/PasswordInput").GetComponent<TMP_InputField>();
        registrationConfirmPasswordInput = registrationContent.Find("Body/ConfirmPasswordRow/ConfirmPasswordInput").GetComponent<TMP_InputField>();
        registrationSubmitButton = registrationContent.Find("Footer/SubmitButton").GetComponent<Button>();
        registrationBackButton = registrationContent.Find("Footer/BackButton").GetComponent<Button>();
        registrationCloseButton = registrationContent.Find("Header/CloseButton").GetComponent<Button>();
        requestFeedback = inputBlocker.GetComponentInChildren<UIRequestFeedback>(true);
        loginResultText = transform.Find("PageRoot/AccountPanel/ResultText").GetComponent<TMP_Text>();
        registrationResultText = registrationContent.Find("ResultText").GetComponent<TMP_Text>();
        showPasswordToggle = passwordInput.transform.Find("VisibilityToggle").GetComponent<Toggle>();
        showRegistrationPasswordToggle = registrationPasswordInput.transform.Find("VisibilityToggle").GetComponent<Toggle>();
        showConfirmationPasswordToggle = registrationConfirmPasswordInput.transform.Find("VisibilityToggle").GetComponent<Toggle>();
        rememberLoginToggle = transform.Find("PageRoot/AccountPanel/LoginOptions/RememberLoginToggle").GetComponent<Toggle>();
        autoLoginToggle = transform.Find("PageRoot/AccountPanel/LoginOptions/AutoLoginToggle").GetComponent<Toggle>();
        localPlayButton = transform.Find("PageRoot/AccountPanel/LocalPlayButton").GetComponent<Button>();
        autoStartServerToggle = transform.Find("PageRoot/AccountPanel/AutoStartServerToggle").GetComponent<Toggle>();
    }

    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
        viewModel = new LoginViewModel(SaveProfileManager.Instance);
        // Prefab 内保留一个隐藏模板，运行时实例只由档案列表生成。
        saveItemTemplate.gameObject.SetActive(false);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        isEnteringProfile = false;
        formRevision++;
        ClearFormFeedback(false);
        ClearFormFeedback(true);
        requestFeedback.HideProcessing();
        if (viewModel.IsAuthenticating)
            ShowFormFeedback(false, "上一次账号请求正在结束，仍可选择本地游玩。", LoginViewModel.CredentialField.None);
#if UNITY_EDITOR_WIN
        autoStartServerToggle.gameObject.SetActive(true);
        autoStartServerToggle.SetIsOnWithoutNotify(CloudSaveServerLauncher.AutoStart);
#else
        autoStartServerToggle.gameObject.SetActive(false);
#endif
        passwordInput.SetTextWithoutNotify(string.Empty);
        registrationPanel.SetActive(false);
        registrationPasswordInput.SetTextWithoutNotify(string.Empty);
        registrationConfirmPasswordInput.SetTextWithoutNotify(string.Empty);
        // 账号入口只读取云存档，不枚举、迁移或加载旧本地档案。
        ResetPasswordVisibility();
        if (!string.IsNullOrEmpty(viewModel.RememberedUsername))
            usernameInput.SetTextWithoutNotify(viewModel.RememberedUsername);
        RestoreLoginOptions();
        if (viewModel.TryBeginAutomaticLogin())
            AuthenticateAccountAsync(false, true).Forget();
    }

    public override void OnAddListener()
    {
        base.OnAddListener();
        loginButton.onClick.AddListener(LoginAccount);
        registerButton.onClick.AddListener(ShowRegistration);
        registrationSubmitButton.onClick.AddListener(RegisterAccount);
        registrationBackButton.onClick.AddListener(ReturnToLogin);
        registrationCloseButton.onClick.AddListener(ReturnToLogin);
        showPasswordToggle.onValueChanged.AddListener(OnShowPasswordChanged);
        showRegistrationPasswordToggle.onValueChanged.AddListener(OnShowRegistrationPasswordChanged);
        showConfirmationPasswordToggle.onValueChanged.AddListener(OnShowConfirmationPasswordChanged);
        rememberLoginToggle.onValueChanged.AddListener(OnRememberLoginChanged);
        autoLoginToggle.onValueChanged.AddListener(OnAutoLoginChanged);
        localPlayButton.onClick.AddListener(StartLocalGame);
        autoStartServerToggle.onValueChanged.AddListener(OnAutoStartServerChanged);
        foreach (TMP_InputField input in new[] { usernameInput, passwordInput, registrationUsernameInput,
            registrationPasswordInput, registrationConfirmPasswordInput })
            input.onValueChanged.AddListener(OnCredentialChanged);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
        loginButton.onClick.RemoveListener(LoginAccount);
        registerButton.onClick.RemoveListener(ShowRegistration);
        registrationSubmitButton.onClick.RemoveListener(RegisterAccount);
        registrationBackButton.onClick.RemoveListener(ReturnToLogin);
        registrationCloseButton.onClick.RemoveListener(ReturnToLogin);
        showPasswordToggle.onValueChanged.RemoveListener(OnShowPasswordChanged);
        showRegistrationPasswordToggle.onValueChanged.RemoveListener(OnShowRegistrationPasswordChanged);
        showConfirmationPasswordToggle.onValueChanged.RemoveListener(OnShowConfirmationPasswordChanged);
        rememberLoginToggle.onValueChanged.RemoveListener(OnRememberLoginChanged);
        autoLoginToggle.onValueChanged.RemoveListener(OnAutoLoginChanged);
        localPlayButton.onClick.RemoveListener(StartLocalGame);
        autoStartServerToggle.onValueChanged.RemoveListener(OnAutoStartServerChanged);
        foreach (TMP_InputField input in new[] { usernameInput, passwordInput, registrationUsernameInput,
            registrationPasswordInput, registrationConfirmPasswordInput })
            input.onValueChanged.RemoveListener(OnCredentialChanged);
    }

    public override void OnClose()
    {
        formRevision++;
        ResetPasswordVisibility();
        requestFeedback.HideProcessing();
        passwordInput.SetTextWithoutNotify(string.Empty);
        EventSystem.current?.SetSelectedGameObject(null);
        base.OnClose();
        registrationPanel.SetActive(false);
        registrationPasswordInput.SetTextWithoutNotify(string.Empty);
        registrationConfirmPasswordInput.SetTextWithoutNotify(string.Empty);
    }

    public override void OnRelease()
    {
        base.OnRelease();
        profileItems.Clear();
    }

    void LoginAccount()
    {
        AuthenticateAccountAsync(false).Forget();
    }

    void RegisterAccount()
    {
        AuthenticateAccountAsync(true).Forget();
    }

    void ShowRegistration()
    {
        if (viewModel.IsAuthenticating || registrationPanel.activeSelf)
        {
            return;
        }

        // 注册和登录共用账号信息，但密码不在两张表单之间传递。
        registrationUsernameInput.SetTextWithoutNotify(usernameInput.text);
        registrationPasswordInput.SetTextWithoutNotify(string.Empty);
        registrationConfirmPasswordInput.SetTextWithoutNotify(string.Empty);
        passwordInput.SetTextWithoutNotify(string.Empty);
        EventSystem.current?.SetSelectedGameObject(null);
        registrationPanel.SetActive(true);
        ResetPasswordVisibility();
        ClearFormFeedback(true);
        registrationUsernameInput.Select();
        registrationUsernameInput.ActivateInputField();
    }

    void ReturnToLogin()
    {
        if (viewModel.IsAuthenticating)
        {
            return;
        }

        registrationPanel.SetActive(false);
        ResetPasswordVisibility();
        registrationPasswordInput.SetTextWithoutNotify(string.Empty);
        registrationConfirmPasswordInput.SetTextWithoutNotify(string.Empty);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void OnCredentialChanged(string value)
    {
        ClearFormFeedback(registrationPanel.activeSelf);
        RefreshRememberedLoginHint();
    }

    void ApplyPasswordVisibility(TMP_InputField input, Toggle toggle)
    {
        input.contentType = toggle.isOn ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
        input.ForceLabelUpdate();
        toggle.GetComponentInChildren<TMP_Text>().text = toggle.isOn ? "隐藏" : "显示";
    }

    void ResetPasswordVisibility()
    {
        showPasswordToggle.SetIsOnWithoutNotify(false);
        showRegistrationPasswordToggle.SetIsOnWithoutNotify(false);
        showConfirmationPasswordToggle.SetIsOnWithoutNotify(false);
        ApplyPasswordVisibility(passwordInput, showPasswordToggle);
        ApplyPasswordVisibility(registrationPasswordInput, showRegistrationPasswordToggle);
        ApplyPasswordVisibility(registrationConfirmPasswordInput, showConfirmationPasswordToggle);
    }

    void OnShowPasswordChanged(bool visible)
    {
        if (viewModel.IsAuthenticating) showPasswordToggle.SetIsOnWithoutNotify(!visible);
        ApplyPasswordVisibility(passwordInput, showPasswordToggle);
    }

    void OnShowRegistrationPasswordChanged(bool visible)
    {
        if (viewModel.IsAuthenticating) showRegistrationPasswordToggle.SetIsOnWithoutNotify(!visible);
        ApplyPasswordVisibility(registrationPasswordInput, showRegistrationPasswordToggle);
    }

    void OnShowConfirmationPasswordChanged(bool visible)
    {
        if (viewModel.IsAuthenticating) showConfirmationPasswordToggle.SetIsOnWithoutNotify(!visible);
        ApplyPasswordVisibility(registrationConfirmPasswordInput, showConfirmationPasswordToggle);
    }

    void OnRememberLoginChanged(bool remember)
    {
        if (!viewModel.IsAuthenticating)
        {
            viewModel.ConfigureLoginOptions(remember, remember && autoLoginToggle.isOn);
            ClearFormFeedback(false);
        }
        RestoreLoginOptions();
    }

    void OnAutoLoginChanged(bool automatic)
    {
        if (!viewModel.IsAuthenticating)
            viewModel.ConfigureLoginOptions(automatic || rememberLoginToggle.isOn, automatic);
        RestoreLoginOptions();
    }

    void RestoreLoginOptions()
    {
        rememberLoginToggle.SetIsOnWithoutNotify(viewModel.RememberLogin);
        autoLoginToggle.SetIsOnWithoutNotify(viewModel.AutoLogin);
        RefreshRememberedLoginHint();
    }

    void OnAutoStartServerChanged(bool enabled)
    {
#if UNITY_EDITOR_WIN
        CloudSaveServerLauncher.AutoStart = enabled;
#endif
    }

    void CancelEntry()
    {
        if (entryCancellation == null || entryCancellation.IsCancellationRequested)
            return;
        // 先作废界面请求，再通知异步链；迟到的结果不能接管新入口。
        formRevision++;
        entryCancellation.Cancel();
        isEnteringProfile = false;
        requestFeedback.HideProcessing();
        ShowFormFeedback(false, "已取消本次进入，可选择本地游玩或稍后重新登录。", LoginViewModel.CredentialField.None);
    }

    void StartLocalGame() => EnterLocalGameAsync().Forget();

    async UniTask EnterLocalGameAsync()
    {
        if (entryCancellation != null && !entryCancellation.IsCancellationRequested)
            return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        entryCancellation = request;
        int revision = ++formRevision;
        isEnteringProfile = true;
        registrationPanel.SetActive(false);
        ClearFormFeedback(false);
        EventSystem.current?.SetSelectedGameObject(null);
        requestFeedback.ShowProcessing("正在读取本地存档…", () => !request.IsCancellationRequested, CancelEntry);
        try
        {
            await viewModel.EnterLocalGameAsync(request.Token);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (revision == formRevision && !request.IsCancellationRequested)
                ShowFormFeedback(false, exception.Message, LoginViewModel.CredentialField.None);
            Debug.LogError($"本地进入失败: {exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(entryCancellation, request))
            {
                entryCancellation = null;
                isEnteringProfile = false;
                if (this != null) requestFeedback.HideProcessing();
            }
        }
    }

    void RefreshRememberedLoginHint()
    {
        ((TMP_Text)passwordInput.placeholder).text = viewModel.CanUseRememberedLogin(usernameInput.text)
            ? "已记住登录，可直接点击登录" : "请输入密码";
    }

    void ClearFormFeedback(bool register)
    {
        (register ? registrationResultText : loginResultText).text = string.Empty;
        var inputs = register
            ? new[] { registrationUsernameInput, registrationPasswordInput, registrationConfirmPasswordInput }
            : new[] { usernameInput, passwordInput };
        foreach (TMP_InputField input in inputs)
            input.GetComponent<Outline>().effectColor = new Color(.83f, .84f, .86f, 1f);
    }

    void ShowFormFeedback(bool register, string message, LoginViewModel.CredentialField field, bool success = false)
    {
        ClearFormFeedback(register);
        TMP_Text result = register ? registrationResultText : loginResultText;
        result.text = message;
        result.color = success ? new Color(.22f, .48f, .34f, 1f) : new Color(.72f, .24f, .24f, 1f);
        TMP_InputField input = field switch
        {
            LoginViewModel.CredentialField.Username => register ? registrationUsernameInput : usernameInput,
            LoginViewModel.CredentialField.Password => register ? registrationPasswordInput : passwordInput,
            LoginViewModel.CredentialField.Confirmation => registrationConfirmPasswordInput,
            _ => null
        };
        if (input != null)
        {
            input.GetComponent<Outline>().effectColor = result.color;
            input.Select();
            input.ActivateInputField();
        }
    }

    async UniTask AuthenticateAccountAsync(bool register, bool automatic = false)
    {
        // 弹窗打开时只接受注册提交，防止键盘或脚本触发底层登录按钮。
        if (viewModel.IsAuthenticating || isEnteringProfile || register != registrationPanel.activeSelf)
        {
            if (viewModel.IsAuthenticating && !isEnteringProfile)
                ShowFormFeedback(register, "上一次账号请求正在结束，请稍后重试；也可选择本地游玩。", LoginViewModel.CredentialField.None);
            return;
        }

        string username = register ? registrationUsernameInput.text : usernameInput.text;
        string password = register ? registrationPasswordInput.text : passwordInput.text;
        string confirmationPassword = register ? registrationConfirmPasswordInput.text : null;
        string operation = register ? "注册" : "登录";
        if (automatic) operation = "自动登录";
        bool useRememberedLogin = !register && string.IsNullOrEmpty(password) && viewModel.CanUseRememberedLogin(username);
        if (!useRememberedLogin && !LoginViewModel.TryValidateCredentials(username, password, register, confirmationPassword,
            out string validationMessage, out var invalidField))
        {
            ShowFormFeedback(register, validationMessage, invalidField);
            return;
        }
        ClearFormFeedback(register);
        int revision = ++formRevision;
        var destroyCancellation = this.GetCancellationTokenOnDestroy();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellation);
        entryCancellation = request;
        using var timeout = automatic ? request.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime) : null;
        var cancellationToken = request.Token;
        requestFeedback.ShowProcessing($"正在{operation}…", () => !request.IsCancellationRequested,
            register ? null : CancelEntry);
        EventSystem.current?.SetSelectedGameObject(null);
        Debug.Log($"开始{operation}。");
        bool preparingCloud = false;

        try
        {
            // 取消同时传给 HTTP 请求；旧页面的结果仍由表单版本守卫隔离。
            UniTask authentication = useRememberedLogin
                ? viewModel.AuthenticateRememberedAsync(username, cancellationToken)
                : viewModel.AuthenticateAsync(username, password, register, confirmationPassword, cancellationToken);
            await authentication
                .AttachExternalCancellation(cancellationToken);
            // 页面重开后，旧请求的结果不能覆盖新表单。
            if (revision != formRevision)
                return;
            if (register)
            {
                usernameInput.SetTextWithoutNotify(username.Trim());
                ReturnToLogin();
                ShowFormFeedback(false, "注册成功，请登录。", LoginViewModel.CredentialField.None, true);
            }
            else
            {
                preparingCloud = true;
                requestFeedback.ShowProcessing("正在准备云存档…", () => !request.IsCancellationRequested, CancelEntry);
                await viewModel.EnterGameAsync(username, cancellationToken)
                    .AttachExternalCancellation(cancellationToken);
                if (revision != formRevision)
                    return;
                ShowFormFeedback(false, "登录成功。", LoginViewModel.CredentialField.None, true);
            }
            passwordInput.SetTextWithoutNotify(string.Empty);
            registrationPasswordInput.SetTextWithoutNotify(string.Empty);
            registrationConfirmPasswordInput.SetTextWithoutNotify(string.Empty);
            Debug.Log($"{operation}成功，PlayerId: {viewModel.PlayerId}");
            if (!register)
                Debug.Log("云存档已就绪，开始进入游戏。");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 本次进入取消后不再调用云端或操作销毁的页面。
            if (!destroyCancellation.IsCancellationRequested && revision == formRevision)
                ShowFormFeedback(false, "自动登录等待已结束，可重试或选择本地游玩。", LoginViewModel.CredentialField.None);
        }
        catch (RequestFailedException exception)
        {
            if (revision == formRevision && !cancellationToken.IsCancellationRequested)
            {
                var field = LoginViewModel.CredentialField.None;
                string message = preparingCloud
                    ? GetCloudFeedback(exception)
                    : LoginViewModel.GetAuthenticationFeedback(exception, register, out field);
                if (!register && field == LoginViewModel.CredentialField.Password)
                    passwordInput.SetTextWithoutNotify(string.Empty);
                ShowFormFeedback(register, message, field);
            }
            Debug.LogError($"{operation}失败，错误码: {exception.ErrorCode}，原因: {exception.Message}");
        }
        catch (Exception exception)
        {
            if (revision == formRevision && !cancellationToken.IsCancellationRequested)
            {
                if (preparingCloud)
                    ShowFormFeedback(false, GetCloudFeedback(exception), LoginViewModel.CredentialField.None);
                else
                    ShowFormFeedback(register, LoginViewModel.GetAuthenticationFeedback(exception, register, out var field), field);
            }
            Debug.LogError($"{operation}失败: {exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(entryCancellation, request))
                entryCancellation = null;
            if (!destroyCancellation.IsCancellationRequested && revision == formRevision)
            {
                requestFeedback.HideProcessing();
                ResetPasswordVisibility();
                RestoreLoginOptions();
            }
        }
    }

    static string GetCloudFeedback(Exception exception)
    {
        if (exception is CloudSaveServerException)
            return exception.Message;
        return "云存档准备失败，请检查网络或存档状态后重试。";
    }

    void RebuildProfileItems()
    {
        foreach (SaveProfileItemView item in profileItems.Values)
        {
            Destroy(item.gameObject);
        }

        profileItems.Clear();
        viewModel.LoadProfiles();
        foreach (SaveProfileInfo profile in viewModel.Profiles)
        {
            AddProfileItem(profile);
        }

        RefreshSelection(null, viewModel.SelectedProfile?.profileId);
    }

    void CreateProfile()
    {
        if (isEnteringProfile)
        {
            return;
        }

        string previousProfileId = viewModel.SelectedProfile?.profileId;
        SaveProfileInfo profile = viewModel.CreateProfile();
        AddProfileItem(profile);
        RefreshSelection(previousProfileId, profile.profileId);
    }

    void SelectProfile(string profileId)
    {
        if (isEnteringProfile)
        {
            return;
        }

        string previousProfileId = viewModel.SelectedProfile?.profileId;
        if (viewModel.SelectProfile(profileId))
        {
            RefreshSelection(previousProfileId, profileId);
        }
    }

    void ConfirmProfile()
    {
        if (isEnteringProfile)
        {
            return;
        }

        if (viewModel.ConfirmSelection())
        {
            Debug.Log($"已选择存档: {viewModel.SelectedProfile.displayName}");
            EnterSelectedProfileAsync(viewModel.SelectedProfile.profileId).Forget();
        }
    }

    async UniTask EnterSelectedProfileAsync(string profileId)
    {
        // 初始化存档期间锁住入口；失败时恢复，成功时由关闭动画接管界面生命周期。
        isEnteringProfile = true;
        inputBlocker.SetActive(true);
        // 遮挡只拦截指针；清除焦点并保留流程守卫，避免键盘提交绕过遮挡。
        EventSystem.current?.SetSelectedGameObject(null);
        Debug.Log("开始登录并读取所选存档。");

        try
        {
            // 这里只取消页面的等待；FlowManager 的登录请求生命周期另行处理。
            bool entered = await FlowManager.Instance.EnterSelectedProfileAsync(profileId)
                .AttachExternalCancellation(this.GetCancellationTokenOnDestroy());
            if (entered)
            {
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // 页面已销毁，不再操作其遮挡和按钮。
            return;
        }

        isEnteringProfile = false;
        inputBlocker.SetActive(false);
        Debug.Log("进入流程未完成，已恢复登录界面操作。");
    }

    void AddProfileItem(SaveProfileInfo profile)
    {
        SaveProfileItemView item = Instantiate(saveItemTemplate, saveListContent);
        item.name = $"SaveProfileItem_{profile.profileId}";
        item.gameObject.SetActive(true);
        item.Bind(profile, SelectProfile);
        profileItems.Add(profile.profileId, item);
    }

    void RefreshSelection(string previousProfileId, string selectedProfileId)
    {
        if (!string.IsNullOrEmpty(previousProfileId) &&
            profileItems.TryGetValue(previousProfileId, out SaveProfileItemView previousItem))
        {
            previousItem.SetSelected(false);
        }

        if (!string.IsNullOrEmpty(selectedProfileId) &&
            profileItems.TryGetValue(selectedProfileId, out SaveProfileItemView selectedItem))
        {
            selectedItem.SetSelected(true);
        }

        enterButton.interactable = !string.IsNullOrEmpty(selectedProfileId);
    }
}
