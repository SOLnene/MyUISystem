using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;

internal sealed class AccountSession
{
    internal static AccountSession Current { get; set; } = new AccountSession(AccountHttpClient.Default);
    readonly AccountHttpClient client;
    readonly SemaphoreSlim refreshLock = new SemaphoreSlim(1, 1);
    AccountLoginResponse session;
    LoginPreferences preferences;
    int generation;
    internal bool IsAuthorized => session != null && session.refreshExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    internal string PlayerId => session?.uid;
    internal string Username => session?.username;

    internal AccountSession(AccountHttpClient transport) { client = transport; }

    internal async UniTask LoginAsync(string username, string password, LoginPreferences loginPreferences, CancellationToken cancellationToken)
    {
        int expected = generation;
        var result = await client.LoginAsync(username, password, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (expected != generation) throw new OperationCanceledException();
        Commit(result, loginPreferences);
    }

    internal async UniTask RestoreAsync(LoginPreferences loginPreferences, CancellationToken cancellationToken)
    {
        string credential = loginPreferences.ReadCredential();
        if (string.IsNullOrEmpty(credential)) throw new AccountServerException(401, "invalid_session");
        int expected = generation;
        AccountLoginResponse result;
        try { result = await client.RefreshAsync(credential, cancellationToken); }
        catch (AccountServerException error) when (error.StatusCode == 401)
        {
            if (expected == generation) loginPreferences.InvalidateAccount();
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (expected != generation) throw new OperationCanceledException();
        Validate(result);
        if (result.uid != loginPreferences.PlayerId || !string.Equals(result.username, loginPreferences.Username, StringComparison.OrdinalIgnoreCase))
        {
            loginPreferences.InvalidateAccount();
            throw new AccountServerException(401, "invalid_session");
        }
        Commit(result, loginPreferences);
    }

    void Commit(AccountLoginResponse result, LoginPreferences loginPreferences)
    {
        Validate(result);
        // Persist only the protected refresh credential; password and access token remain outside settings.
        loginPreferences.StoreCredential(result.refreshToken);
        loginPreferences.RecordAccount(result.username, result.uid);
        session = result;
        preferences = loginPreferences;
        generation++;
    }

    internal void ConfigureRemember(LoginPreferences loginPreferences)
    {
        if (!IsAuthorized || !loginPreferences.RememberLogin) return;
        loginPreferences.StoreCredential(session.refreshToken);
        loginPreferences.RecordAccount(session.username, session.uid);
        preferences = loginPreferences;
    }

    internal async UniTask<string> GetAccessTokenAsync(string playerId, CancellationToken cancellationToken)
    {
        RequireOwner(playerId);
        await refreshLock.WaitAsync(cancellationToken).AsUniTask();
        try
        {
            RequireOwner(playerId);
            if (session.accessExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds())
            {
                int expected = generation;
                AccountLoginResponse result;
                try { result = await client.RefreshAsync(session.refreshToken, cancellationToken); }
                catch (AccountServerException error) when (error.StatusCode == 401)
                {
                    if (expected == generation) Clear(true);
                    throw new CloudSaveServerException(401, "unauthorized");
                }
                cancellationToken.ThrowIfCancellationRequested();
                // A refresh finishing after account switch/logout cannot restore the old identity.
                if (expected != generation) throw new CloudSaveServerException(401, "unauthorized");
                Validate(result);
                if (result.uid != playerId) { Clear(true); throw new CloudSaveServerException(401, "unauthorized"); }
                session = result;
            }
            return session.accessToken;
        }
        finally { refreshLock.Release(); }
    }

    internal async UniTask LogoutAsync(CancellationToken cancellationToken = default)
    {
        string credential = session?.refreshToken;
        Clear(true);
        if (!string.IsNullOrEmpty(credential)) await client.LogoutAsync(credential, cancellationToken);
    }

    internal void Clear(bool forgetCredential)
    {
        generation++;
        session = null;
        if (forgetCredential) preferences?.InvalidateAccount();
    }

    void RequireOwner(string playerId)
    {
        if (!IsAuthorized || playerId != PlayerId) throw new CloudSaveServerException(401, "unauthorized");
    }

    static void Validate(AccountLoginResponse result)
    {
        if (result == null || !long.TryParse(result.uid, out long uid) || uid < 100000001 ||
            string.IsNullOrEmpty(result.username) || result.accessToken == null || result.accessToken.Length != 67 ||
            !result.accessToken.StartsWith("as_", StringComparison.Ordinal) || result.refreshToken == null || result.refreshToken.Length != 67 ||
            !result.refreshToken.StartsWith("rs_", StringComparison.Ordinal) ||
            result.accessExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || result.refreshExpiresAt < result.accessExpiresAt)
            throw new InvalidDataException("账号服务返回的身份或凭据无效。");
    }
}
