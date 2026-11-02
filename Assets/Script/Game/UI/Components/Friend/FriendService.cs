using System;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

internal sealed class SocialPlayer
{
    public string uid, name, relationship;
    internal FriendInfo ToFriend(bool request = false) => new(new PlayerSummaryInfo(name, uid), 0, string.Empty, string.Empty) { IsRequest = request };
}
internal sealed class SocialState { public SocialPlayer[] friends, requests; }
internal sealed class SocialMessage { public long id; public string senderUid, clientId, text, sentAtUtc; }
internal sealed class SocialSend { public string clientId = Guid.NewGuid().ToString("D"); public string text; }

internal sealed partial class CloudSaveHttpClient
{
    internal async UniTask<T> SocialAsync<T>(string path, object body, CancellationToken token)
    {
        var account = AccountSession.Current;
        if (!account.IsAuthorized) throw new InvalidOperationException("请先登录账号后使用好友功能。");
        using var response = await SendAsync(body == null ? HttpMethod.Get : HttpMethod.Post, account.PlayerId,
            body == null ? null : JsonConvert.SerializeObject(body), token, new Uri(saveUri, "/v1/" + path));
        string json = await response.Content.ReadAsStringAsync().AsUniTask();
        EnsureSuccess(response, json); token.ThrowIfCancellationRequested();
        return JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidOperationException("好友服务返回的数据无效。");
    }
}
