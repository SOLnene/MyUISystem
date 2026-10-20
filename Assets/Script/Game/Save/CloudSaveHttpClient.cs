using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal sealed partial class CloudSaveHttpClient
{
    internal const string ServiceUrl = "http://127.0.0.1:5127";
    internal static CloudSaveHttpClient Default { get; } = new CloudSaveHttpClient(ServiceUrl);
    readonly HttpClient http;
    readonly Uri saveUri;
    readonly Uri gachaUri;

    internal CloudSaveHttpClient(string serviceUrl, HttpClient httpClient = null)
    {
        var origin = new Uri(serviceUrl.TrimEnd('/') + "/", UriKind.Absolute);
        if (origin.Scheme != Uri.UriSchemeHttps && !(origin.Scheme == Uri.UriSchemeHttp && origin.IsLoopback))
            throw new ArgumentException("云存档地址必须使用 HTTPS，本机测试可使用回环 HTTP。", nameof(serviceUrl));
        saveUri = new Uri(origin, "v1/save");
        gachaUri = new Uri(origin, "v1/gacha/config");
        // 当前 Windows 入口使用托管 HTTP；不放宽 Unity 全局 HTTP 策略，也不随重定向发送凭据。
        http = httpClient ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = !origin.IsLoopback
        })
        {
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 8 * 1024 * 1024
        };
    }

    internal async UniTask<CloudSaveSnapshot> LoadAsync(string playerId, CancellationToken cancellationToken = default)
    {
#if UNITY_EDITOR_WIN
        await CloudSaveServerLauncher.EnsureRunningAsync(cancellationToken);
#endif
        using var response = await SendAsync(HttpMethod.Get, playerId, null, cancellationToken);
        string body = await response.Content.ReadAsStringAsync().AsUniTask();
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // 只有明确的业务不存在响应才允许初始化；未知 404 也可能是地址配置错误。
            if (ReadError(body)?.code == "save_not_found")
                return null;
            throw new CloudSaveServerException(404, "invalid_endpoint");
        }
        EnsureSuccess(response, body);
        var snapshot = ReadResponse<CloudSaveSnapshot>(body);
        if (string.IsNullOrWhiteSpace(snapshot.saveJson) || snapshot.revision <= 0 ||
            string.IsNullOrWhiteSpace(snapshot.updatedAtUtc))
            throw new InvalidDataException("云存档服务返回的内容或版本无效，未创建替代存档。");
        return snapshot;
    }

    internal async UniTask<CloudSaveReceipt> SaveAsync(string playerId, CloudSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Put, playerId, JsonUtility.ToJson(request), cancellationToken);
        string body = await response.Content.ReadAsStringAsync().AsUniTask();
        EnsureSuccess(response, body);
        var receipt = ReadResponse<CloudSaveReceipt>(body);
        if (receipt.revision != checked(request.expectedRevision + 1) || string.IsNullOrWhiteSpace(receipt.updatedAtUtc))
            throw new InvalidDataException("云存档服务未返回有效的写入确认，保留原请求等待重试。");
        return receipt;
    }

    internal async UniTask<GachaConfigSnapshot> LoadGachaConfigAsync(string playerId, CancellationToken cancellationToken)
    {
#if UNITY_EDITOR_WIN
        await CloudSaveServerLauncher.EnsureRunningAsync(cancellationToken);
#endif
        using var response = await SendAsync(HttpMethod.Get, playerId, null, cancellationToken, gachaUri);
        string body = await response.Content.ReadAsStringAsync().AsUniTask();
        EnsureSuccess(response, body);
        cancellationToken.ThrowIfCancellationRequested();
        return RemoteGachaConfiguration.ParseSnapshot(body);
    }

    async UniTask<HttpResponseMessage> SendAsync(HttpMethod method, string playerId, string json,
        CancellationToken cancellationToken, Uri requestUri = null)
    {
        string accessToken = await AccountSession.Current.GetAccessTokenAsync(playerId, cancellationToken);
        using var request = new HttpRequestMessage(method, requestUri ?? saveUri);
        // 账号会话在需要时刷新访问凭据；存档和卡池请求不发送密码或刷新凭据。
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (json != null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        try
        {
            return await http.SendAsync(request, cancellationToken).AsUniTask();
        }
        catch (HttpRequestException)
        {
            throw new CloudSaveServerException(0, "connection_failed");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CloudSaveServerException(0, "request_timeout");
        }
    }

    static T ReadResponse<T>(string body) where T : class
    {
        try
        {
            var result = JsonUtility.FromJson<T>(body);
            if (result != null)
                return result;
        }
        catch (ArgumentException) { }
        throw new InvalidDataException("云存档服务响应无效，未创建或覆盖存档。");
    }

    static CloudSaveError ReadError(string body)
    {
        try { return JsonUtility.FromJson<CloudSaveError>(body); }
        catch (ArgumentException) { return null; }
    }

    static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (!response.IsSuccessStatusCode)
            throw new CloudSaveServerException((int)response.StatusCode, ReadError(body)?.code);
    }
}

[Serializable]
internal sealed class CloudSaveSnapshot
{
    public string saveJson = null;
    public long revision = 0;
    public string updatedAtUtc = null;
}

[Serializable]
internal sealed class CloudSaveWriteRequest
{
    public long expectedRevision;
    public string requestId;
    public string saveJson;

    internal CloudSaveWriteRequest(long revision, string json)
    {
        expectedRevision = revision;
        requestId = Guid.NewGuid().ToString("D");
        saveJson = json;
    }
}

[Serializable]
internal sealed class CloudSaveReceipt
{
    public long revision = 0;
    public string updatedAtUtc = null;
    public bool replayed = false;
}

[Serializable]
internal sealed class CloudSaveError
{
    public string code = null;
}

internal sealed class CloudSaveServerException : Exception
{
    internal int StatusCode { get; }
    internal string Code { get; }

    internal CloudSaveServerException(int statusCode, string code)
        : base(GetFeedback(statusCode, code))
    {
        StatusCode = statusCode;
        Code = code;
    }

    static string GetFeedback(int statusCode, string code)
    {
        if (code == "connection_failed")
            return "无法连接云存档服务器，请确认服务器已启动后重试。";
        if (code == "request_timeout")
            return "云存档请求超时，请稍后重试；未确认的存档仍保留待保存。";
        switch (statusCode)
        {
            case 401: return "登录凭据已失效或不属于当前服务，请重新登录。";
            case 403: return "云存档服务拒绝访问，请检查服务配置。";
            case 404: return "云存档服务地址无效，请检查配置；未创建替代存档。";
            case 409: return "云存档已被其他设备更新或请求冲突，请重新启动并登录。";
            case 400:
            case 413: return "存档内容不符合服务器要求，未覆盖云端存档。";
            case 429: return "云存档请求过于频繁，请稍后重试。";
            case 503: return "云存档服务暂时不可用，请稍后重试。";
            default: return $"云存档服务请求失败（HTTP {statusCode}），请稍后重试。";
        }
    }
}
