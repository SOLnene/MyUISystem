using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

internal sealed partial class CloudSaveHttpClient
{
    internal async UniTask<CloudStoreSnapshot> LoadStoreAsync(string playerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, playerId, null, cancellationToken,
            new Uri(saveUri, "/v1/store"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask();
        EnsureSuccess(response, body);
        var snapshot = JsonConvert.DeserializeObject<CloudStoreSnapshot>(body);
        if (snapshot == null) throw new InvalidDataException("商店返回了空配置，请稍后重试。");
        snapshot.Validate();
        return snapshot;
    }

    internal async UniTask<CloudStoreReply> PurchaseStoreAsync(string playerId, CloudStoreRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, playerId, JsonConvert.SerializeObject(request),
            cancellationToken, new Uri(saveUri, "/v1/store/purchase"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask();
        EnsureSuccess(response, body);
        var reply = JsonConvert.DeserializeObject<CloudStoreReply>(body);
        var operation = reply?.operation;
        if (operation == null || reply.state == null || operation.requestId != request.requestId ||
            operation.storeItemId != request.storeItemId || operation.count != request.count ||
            operation.configVersion != request.configVersion || operation.itemId <= 0 ||
            operation.grantedAmount <= 0 || operation.costItemId <= 0 || operation.totalPrice < 0 ||
            operation.committedRevision != checked(request.expectedRevision + 1) ||
            reply.state.revision < operation.committedRevision || string.IsNullOrWhiteSpace(reply.state.saveJson))
            throw new InvalidDataException("服务器返回的购买回执无效，请重新登录恢复购买结果。");
        return reply;
    }
}

[Serializable]
internal sealed class CloudStoreRequest
{
    public string requestId;
    public int storeItemId;
    public int count;
    public long expectedRevision;
    public string configVersion;

    internal bool IsValid => Guid.TryParseExact(requestId, "D", out var id) && id != Guid.Empty &&
        storeItemId > 0 && count > 0 && count <= 1000000 && expectedRevision > 0 &&
        expectedRevision < long.MaxValue && configVersion != null && configVersion.Length == 64;
}
[Serializable]
internal sealed class CloudStoreReply
{
    public CloudStoreOperation operation;
    public CloudSaveSnapshot state;
    public bool replayed;
}
[Serializable]
internal sealed class CloudStoreOperation
{
    public string requestId;
    public int storeItemId;
    public int count;
    public string configVersion;
    public long committedRevision;
    public int itemId;
    public int grantedAmount;
    public int costItemId;
    public int totalPrice;
}
