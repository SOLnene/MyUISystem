using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal sealed class AccountHttpClient
{
    internal static AccountHttpClient Default { get; } = new AccountHttpClient(CloudSaveHttpClient.ServiceUrl);
    readonly HttpClient http;
    readonly Uri origin;

    internal AccountHttpClient(string serviceUrl, HttpClient httpClient = null)
    {
        origin = new Uri(serviceUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if (origin.Scheme != Uri.UriSchemeHttps && !(origin.Scheme == Uri.UriSchemeHttp && origin.IsLoopback))
            throw new ArgumentException("账号服务地址必须使用 HTTPS，本机测试可使用回环 HTTP。", nameof(serviceUrl));
        http = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = !origin.IsLoopback })
        { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 64 * 1024 };
    }

    internal async UniTask<AccountIdentityResponse> RegisterAsync(string username, string password, CancellationToken cancellationToken) =>
        Read<AccountIdentityResponse>(await PostAsync("register", JsonUtility.ToJson(new AccountCredentialsRequest { username = username, password = password }), cancellationToken));

    internal async UniTask<AccountLoginResponse> LoginAsync(string username, string password, CancellationToken cancellationToken) =>
        Read<AccountLoginResponse>(await PostAsync("login", JsonUtility.ToJson(new AccountCredentialsRequest { username = username, password = password }), cancellationToken));

    internal async UniTask<AccountLoginResponse> RefreshAsync(string credential, CancellationToken cancellationToken) =>
        Read<AccountLoginResponse>(await PostAsync("refresh", JsonUtility.ToJson(new AccountRefreshRequest { refreshToken = credential }), cancellationToken));

    internal async UniTask LogoutAsync(string credential, CancellationToken cancellationToken) =>
        await PostAsync("logout", JsonUtility.ToJson(new AccountRefreshRequest { refreshToken = credential }), cancellationToken);

    async UniTask<string> PostAsync(string operation, string json, CancellationToken cancellationToken)
    {
#if UNITY_EDITOR_WIN
        await CloudSaveServerLauncher.EnsureRunningAsync(cancellationToken);
#endif
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "v1/auth/" + operation));
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).AsUniTask();
            string body = await response.Content.ReadAsStringAsync().AsUniTask();
            cancellationToken.ThrowIfCancellationRequested();
            if (!response.IsSuccessStatusCode)
            {
                AccountErrorResponse error = null;
                try { error = JsonUtility.FromJson<AccountErrorResponse>(body); } catch (ArgumentException) { }
                throw new AccountServerException((int)response.StatusCode, error?.code);
            }
            return body;
        }
        catch (HttpRequestException) { throw new AccountServerException(0, "connection_failed"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new AccountServerException(0, "request_timeout"); }
    }

    static T Read<T>(string body) where T : class
    {
        try
        {
            var result = JsonUtility.FromJson<T>(body);
            if (result != null) return result;
        }
        catch (ArgumentException) { }
        throw new InvalidDataException("账号服务响应无效，请重试。");
    }
}

[Serializable]
internal sealed class AccountCredentialsRequest { public string username; public string password; }
[Serializable]
internal sealed class AccountRefreshRequest { public string refreshToken; }
[Serializable]
internal sealed class AccountIdentityResponse { public string uid = null; public string username = null; }
[Serializable]
internal sealed class AccountLoginResponse
{
    public string uid = null;
    public string username = null;
    public string accessToken = null;
    public string refreshToken = null;
    public long accessExpiresAt = 0;
    public long refreshExpiresAt = 0;
}
[Serializable]
internal sealed class AccountErrorResponse { public string code = null; }

internal sealed class AccountServerException : Exception
{
    internal int StatusCode { get; }
    internal string Code { get; }
    internal AccountServerException(int statusCode, string code) : base($"Account request failed ({statusCode}, {code}).")
    { StatusCode = statusCode; Code = code; }
}
