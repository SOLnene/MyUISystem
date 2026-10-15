using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class LoginViewModel
{
    readonly SaveProfileManager profileManager;
    readonly LoginPreferences loginPreferences;
    readonly List<SaveProfileInfo> profiles = new List<SaveProfileInfo>();

    // 跨页面实例保留请求守卫，取消时等待 HTTP 请求结束后再允许新请求。
    static bool isAuthenticating;
    static bool isEnteringGame;
    static bool automaticLoginAttempted;
    string lastAuthenticatedUsername;
    string lastAuthenticatedPlayerId;
    internal bool IsAuthenticating => isAuthenticating || isEnteringGame || (CloudGameSaveSession.Current?.IsSaving ?? false);
    internal string PlayerId => AccountSession.Current.PlayerId ?? lastAuthenticatedPlayerId;
    internal bool RememberLogin => loginPreferences.RememberLogin;
    internal bool AutoLogin => loginPreferences.AutoLogin;
    internal string RememberedUsername => loginPreferences.Username;

    public IReadOnlyList<SaveProfileInfo> Profiles => profiles;
    // SelectedProfile 只表示界面选择；确认后才会成为 SaveProfileManager.ActiveProfile。
    public SaveProfileInfo SelectedProfile { get; private set; }

    public LoginViewModel(SaveProfileManager profileManager)
        : this(profileManager, new LoginPreferences())
    {
    }

    internal LoginViewModel(SaveProfileManager profileManager, LoginPreferences preferences)
    {
        this.profileManager = profileManager;
        loginPreferences = preferences;
    }

    internal bool CanUseRememberedLogin(string username)
    {
        return loginPreferences.HasAccount &&
            string.Equals(username?.Trim(), loginPreferences.Username, StringComparison.OrdinalIgnoreCase);
    }

    internal bool TryBeginAutomaticLogin()
    {
        // 每次应用启动最多自动尝试一次，返回页面或请求失败不会形成重试循环。
        if (IsAuthenticating || automaticLoginAttempted || loginPreferences.PreferLocal || !AutoLogin || !loginPreferences.HasAccount)
            return false;
        automaticLoginAttempted = true;
        return true;
    }

    internal void ConfigureLoginOptions(bool remember, bool automatic)
    {
        if (IsAuthenticating)
            return;
        loginPreferences.SetOptions(remember, automatic);
        AccountSession.Current.ConfigureRemember(loginPreferences);
    }

    internal async UniTask AuthenticateAsync(string username, string password, bool register, string confirmationPassword = null,
        CancellationToken cancellationToken = default)
    {
        if (IsAuthenticating)
        {
            throw new InvalidOperationException("已有账号请求正在执行，请稍后重试。");
        }

        username = username?.Trim();
        if (string.IsNullOrEmpty(username))
        {
            throw new ArgumentException("请输入账号。", nameof(username));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("请输入密码。", nameof(password));
        }

        // 确认密码只做本地校验，不能作为凭据发送给认证服务。
        if (register && password != confirmationPassword)
        {
            throw new ArgumentException("两次输入的密码不一致。", nameof(confirmationPassword));
        }

        isAuthenticating = true;
        try
        {
            if (register)
            {
                var account = await AccountHttpClient.Default.RegisterAsync(username, password, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (account == null || !long.TryParse(account.uid, out long uid) || uid < 100000001 ||
                    string.IsNullOrEmpty(account.username))
                    throw new InvalidOperationException("账号服务返回的注册身份无效。");
                lastAuthenticatedUsername = account.username;
                lastAuthenticatedPlayerId = account.uid;
            }
            else
            {
                await AccountSession.Current.LoginAsync(username, password, loginPreferences, cancellationToken);
                lastAuthenticatedUsername = AccountSession.Current.Username;
                lastAuthenticatedPlayerId = AccountSession.Current.PlayerId;
            }
        }
        finally
        {
            isAuthenticating = false;
        }
    }

    internal async UniTask AuthenticateRememberedAsync(string username, CancellationToken cancellationToken = default)
    {
        if (IsAuthenticating || !CanUseRememberedLogin(username))
            throw new InvalidOperationException("已记住的登录不可用，请输入密码。");

        isAuthenticating = true;
        try
        {
            await AccountSession.Current.RestoreAsync(loginPreferences, cancellationToken);
            lastAuthenticatedUsername = AccountSession.Current.Username;
            lastAuthenticatedPlayerId = AccountSession.Current.PlayerId;
        }
        finally
        {
            isAuthenticating = false;
        }
    }

    internal async UniTask EnterGameAsync(string username, CancellationToken cancellationToken)
    {
        if (IsAuthenticating)
            throw new InvalidOperationException("已有账号请求正在执行，请稍后重试。");
        isEnteringGame = true;
        try
        {
            await FlowManager.Instance.EnterCloudGameAsync(username.Trim(), cancellationToken);
            loginPreferences.SelectLocalMode(false);
        }
        finally
        {
            isEnteringGame = false;
        }
    }

    internal async UniTask EnterLocalGameAsync(CancellationToken cancellationToken)
    {
        // 本地模式只等待旧的会话进入操作收尾，不需要账号服务。
        await UniTask.WaitUntil(() => !isEnteringGame, cancellationToken: cancellationToken);
        isEnteringGame = true;
        try
        {
            loginPreferences.SelectLocalMode(true);
            if (!profileManager.TryGetProfile(loginPreferences.LocalProfileId, out var profile, out _))
            {
                profile = profileManager.CreateLocalProfile();
                loginPreferences.RecordLocalProfile(profile.profileId);
            }
            bool entered = await FlowManager.Instance.EnterSelectedProfileAsync(profile.profileId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!entered)
                throw new InvalidOperationException("本地存档进入失败，请检查存档状态后重试。");
        }
        finally { isEnteringGame = false; }
    }

    internal enum CredentialField { None, Username, Password, Confirmation }

    internal static bool TryValidateCredentials(string username, string password, bool register,
        string confirmationPassword, out string message, out CredentialField field)
    {
        message = string.Empty;
        field = CredentialField.Username;
        username = username?.Trim();
        if (string.IsNullOrEmpty(username))
        {
            message = "请输入账号。";
            return false;
        }
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_.@-]{3,20}$"))
        {
            message = "账号需为 3–20 位英文、数字或 .-@_。";
            return false;
        }

        field = CredentialField.Password;
        if (string.IsNullOrEmpty(password))
        {
            message = "请输入密码。";
            return false;
        }
        // 强度预检只用于创建新账号，登录仍由认证服务校验凭据。
        if (register)
        {
            if (password.Length < 8 || password.Length > 64)
            {
                message = "密码需为 8–64 位。";
                return false;
            }
            field = CredentialField.Confirmation;
            if (string.IsNullOrEmpty(confirmationPassword))
            {
                message = "请再次输入密码。";
                return false;
            }
            if (password != confirmationPassword)
            {
                message = "两次输入的密码不一致。";
                return false;
            }
        }
        field = CredentialField.None;
        return true;
    }

    internal static string GetAuthenticationFeedback(Exception exception, bool register, out CredentialField field)
    {
        field = CredentialField.None;
        if (exception is AccountServerException request)
        {
            if (request.Code == "invalid_credentials")
            {
                field = CredentialField.Password;
                return "账号或密码不正确，请重新输入。";
            }
            if (request.Code == "invalid_password")
            {
                field = CredentialField.Password;
                return "密码需为 8–64 位。";
            }
            if (request.Code == "invalid_username")
            {
                field = CredentialField.Username;
                return "账号需为 3–20 位英文、数字或 .-@_。";
            }
            if (request.Code == "invalid_session" || request.StatusCode == 401)
            {
                field = CredentialField.Password;
                return "已记住的登录已失效，请重新输入密码。";
            }
            if (register && request.Code == "username_taken")
            {
                field = CredentialField.Username;
                return "账号已被使用，请更换账号。";
            }
            if (request.Code == "connection_failed")
                return "无法连接账号服务器，请确认服务器已启动后重试。";
            if (request.Code == "request_timeout")
                return "账号请求超时，请稍后重试。";
            if (request.StatusCode == 429)
                return "操作过于频繁，请稍后重试。";
            if (request.StatusCode >= 500)
                return "账号服务暂时不可用，请稍后重试。";
        }
        if (exception is PlatformNotSupportedException) return exception.Message;
        return register ? "注册未完成，请稍后重试。" : "登录未完成，请稍后重试。";
    }

    public void LoadProfiles()
    {
        profiles.Clear();
        profiles.AddRange(profileManager.LoadProfiles());
        SelectedProfile = profiles.Count > 0 ? profiles[0] : null;
    }

    public SaveProfileInfo CreateProfile()
    {
        SaveProfileInfo profile = profileManager.CreateProfile();
        profiles.Add(profile);
        SelectedProfile = profile;
        return profile;
    }

    public bool SelectProfile(string profileId)
    {
        foreach (SaveProfileInfo profile in profiles)
        {
            if (profile.profileId != profileId)
            {
                continue;
            }

            SelectedProfile = profile;
            return true;
        }

        return false;
    }

    public bool ConfirmSelection()
    {
        return SelectedProfile != null;
    }
}
