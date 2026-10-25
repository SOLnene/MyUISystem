using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

internal sealed partial class CloudSaveHttpClient
{
    internal string GachaJournalKey(string playerId)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
            saveUri.AbsoluteUri + "\n" + playerId))).Replace("-", "").ToLowerInvariant();
    }

    internal async UniTask<CloudGachaReply> DrawAsync(string playerId, CloudGachaRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, playerId, JsonConvert.SerializeObject(request),
            cancellationToken, new Uri(saveUri, "/v1/gacha/draw"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask();
        EnsureSuccess(response, body);
        return ReadGachaReply(body, request);
    }

    internal async UniTask<CloudGachaReply> FindDrawAsync(string playerId, CloudGachaRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, playerId, null, cancellationToken,
            new Uri(saveUri, "/v1/gacha/requests/" + request.requestId));
        string body = await response.Content.ReadAsStringAsync().AsUniTask();
        if (response.StatusCode == HttpStatusCode.NotFound && ReadError(body)?.code == "gacha_request_not_found")
            return null;
        EnsureSuccess(response, body);
        return ReadGachaReply(body, request);
    }

    static CloudGachaReply ReadGachaReply(string body, CloudGachaRequest request)
    {
        var reply = ReadResponse<CloudGachaReply>(body);
        var operation = reply.operation;
        if (operation == null || reply.state == null || operation.requestId != request.requestId ||
            operation.gachaKey != request.gachaKey || operation.count != request.count ||
            operation.configVersion != request.configVersion || operation.results == null ||
            operation.results.Length != request.count ||
            operation.committedRevision != checked(request.expectedRevision + 1) ||
            reply.state.revision < operation.committedRevision || string.IsNullOrWhiteSpace(reply.state.saveJson))
            throw new InvalidDataException("服务器返回的抽卡回执无效，已保留原请求等待确认。");
        if ((request.paymentVersion == 1 && operation.payment == null) ||
            (operation.payment != null && !operation.payment.IsValid(request.count, request.maxPrimogemCost)))
            throw new InvalidDataException("抽卡扣费回执无效，已保留原请求等待确认。");
        return reply;
    }
}

[Serializable]
internal sealed class CloudGachaRequest
{
    public string requestId;
    public string gachaKey;
    public int count;
    public long expectedRevision;
    public long configVersion;
    // Omitting zero values preserves the fingerprint of pre-payment journals and receipts.
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int paymentVersion;
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] public int maxPrimogemCost;

    internal bool IsValid => Guid.TryParseExact(requestId, "D", out var id) && id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(gachaKey) && (count == 1 || count == 10) &&
        expectedRevision > 0 && expectedRevision < long.MaxValue && configVersion > 0 &&
        (paymentVersion == 0 || paymentVersion == 1) && maxPrimogemCost >= 0;
}

[Serializable]
internal sealed class CloudGachaReply
{
    public CloudGachaOperation operation;
    public CloudSaveSnapshot state;
    public bool replayed;
}

[Serializable]
internal sealed class CloudGachaOperation
{
    public string requestId;
    public string gachaKey;
    public int count;
    public long configVersion;
    public long committedRevision;
    public CloudGachaAward[] results;
    public CloudGachaPayment payment;
}

[Serializable]
internal sealed class CloudGachaPayment
{
    public int fateItemId;
    public int fatesSpent;
    public int primogemsSpent;
    public int fatesRemaining;
    public int primogemsRemaining;
    public int primogemsPerFate;

    internal bool IsValid(int count, int limit) => (fateItemId == 223 || fateItemId == 224) &&
        primogemsPerFate > 0 && primogemsPerFate <= 1000000 && fatesSpent >= 0 && fatesSpent <= count &&
        primogemsSpent >= 0 && primogemsSpent <= limit && fatesRemaining >= 0 && primogemsRemaining >= 0 &&
        primogemsSpent == (long)(count - fatesSpent) * primogemsPerFate;
}

[Serializable]
internal sealed class CloudGachaAward
{
    public string entryKey;
    public int entryType;
    public int rarity;
    public long instanceId;
    public bool newCharacter;
}
