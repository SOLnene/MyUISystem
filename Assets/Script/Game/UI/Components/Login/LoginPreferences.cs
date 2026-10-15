using System;
using System.ComponentModel;
using UnityEngine;

internal sealed class LoginPreferences
{
    internal const string DefaultProfile = "myuisystem_account";
    const string DefaultPrefix = "MyUISystem.Login.";
    readonly string prefix;
    internal string Profile { get; }
    internal bool RememberLogin => PlayerPrefs.GetInt(prefix + "Remember", 0) != 0;
    internal bool AutoLogin => RememberLogin && PlayerPrefs.GetInt(prefix + "AutoLogin", 0) != 0;
    internal string Username => PlayerPrefs.GetString(prefix + "Local.Username", string.Empty);
    internal string PlayerId => PlayerPrefs.GetString(prefix + "Local.PlayerId", string.Empty);
    internal bool HasAccount => RememberLogin && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(PlayerId) &&
        PlayerPrefs.HasKey(prefix + "Local.Credential");
    internal bool PreferLocal => PlayerPrefs.GetInt(prefix + "PreferLocal", 0) != 0;
    internal string LocalProfileId => PlayerPrefs.GetString(prefix + "LocalProfile", string.Empty);

    internal void RecordLocalProfile(string profileId)
    {
        PlayerPrefs.SetString(prefix + "LocalProfile", profileId);
        PlayerPrefs.Save();
    }

    internal void SelectLocalMode(bool local)
    {
        PlayerPrefs.SetInt(prefix + "PreferLocal", local ? 1 : 0);
        PlayerPrefs.Save();
    }

    static LoginPreferences()
    {
        // 未选择记住时只保留本次进程的凭据，正常退出后清理内存会话。
        Application.quitting += ClearTransientSession;
    }

    internal LoginPreferences(string keyPrefix = DefaultPrefix, string profile = DefaultProfile)
    {
        prefix = keyPrefix;
        Profile = profile;
    }

    internal void SetOptions(bool remember, bool automatic)
    {
        PlayerPrefs.SetInt(prefix + "Remember", remember ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "AutoLogin", remember && automatic ? 1 : 0);
        if (!remember)
        {
            PlayerPrefs.DeleteKey(prefix + "Local.Username");
            PlayerPrefs.DeleteKey(prefix + "Local.PlayerId");
            PlayerPrefs.DeleteKey(prefix + "Local.Credential");
        }
        PlayerPrefs.Save();
    }

    internal void RecordAccount(string username, string playerId)
    {
        if (!RememberLogin)
            return;

        // 这里只记已通过认证的身份；密码不保存，刷新凭据另经系统保护后保存。
        PlayerPrefs.SetString(prefix + "Local.Username", username);
        PlayerPrefs.SetString(prefix + "Local.PlayerId", playerId);
        PlayerPrefs.Save();
    }

    internal void InvalidateAccount()
    {
        PlayerPrefs.SetInt(prefix + "AutoLogin", 0);
        PlayerPrefs.DeleteKey(prefix + "Local.Username");
        PlayerPrefs.DeleteKey(prefix + "Local.PlayerId");
        PlayerPrefs.DeleteKey(prefix + "Local.Credential");
        PlayerPrefs.Save();
    }

    internal void StoreCredential(string credential)
    {
        if (!RememberLogin) return;
        PlayerPrefs.SetString(prefix + "Local.Credential", AccountCredentialProtection.Protect(credential));
        PlayerPrefs.Save();
    }

    internal string ReadCredential()
    {
        if (!HasAccount) return null;
        try { return AccountCredentialProtection.Unprotect(PlayerPrefs.GetString(prefix + "Local.Credential")); }
        catch (Exception error) when (error is FormatException || error is Win32Exception || error is PlatformNotSupportedException)
        { InvalidateAccount(); return null; }
    }

    static void ClearTransientSession()
    {
        if (PlayerPrefs.GetInt(DefaultPrefix + "Remember", 0) == 0)
            AccountSession.Current.Clear(false);
    }
}

