using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Promote;
using Newtonsoft.Json;

internal sealed partial class CloudSaveHttpClient
{
    internal async UniTask<CloudPromoteConfig> LoadPromoteConfigAsync(string player, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Get, player, null, token, new Uri(saveUri, "/v1/progression/promote/config"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        var config = JsonConvert.DeserializeObject<CloudPromoteConfig>(body);
        if (config?.rules == null || config.configVersion?.Length != 64) throw new InvalidDataException("Invalid promotion config.");
        config.rules.Validate(); return config;
    }
    internal async UniTask<CloudPromoteReply> SendPromotionAsync(string player, CloudPromoteRequest request, bool query, CancellationToken token)
    {
        using var response = await SendAsync(query ? HttpMethod.Get : HttpMethod.Post, player, query ? null : JsonConvert.SerializeObject(request), token,
            new Uri(saveUri, "/v1/progression/promote" + (query ? "/requests/" + request.requestId : "")));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        var reply = JsonConvert.DeserializeObject<CloudPromoteReply>(body); var op = reply?.operation;
        if (op == null || reply.state == null || op.requestId != request.requestId || op.target?.IsValid != true ||
            op.target.Identity != request.target.Identity || op.configVersion != request.configVersion ||
            op.committedRevision != checked(request.expectedRevision + 1) || reply.state.revision < op.committedRevision ||
            op.oldRank < 0 || op.rank != op.oldRank + 1 || op.rank > 6 || op.exp < 0 || op.oldExp < 0 || op.goldCost < 0 ||
            op.materials == null || op.materials.Length == 0 || op.materials.Any(m => m == null || m.ItemId <= 0 || m.Count <= 0) ||
            op.materials.Select(m => m.ItemId).Distinct().Count() != op.materials.Length || string.IsNullOrWhiteSpace(reply.state.saveJson))
            throw new InvalidDataException("Invalid promotion receipt.");
        return reply;
    }
}
internal sealed class CloudPromoteConfig { public string configVersion; public PromotionRules rules; }
internal sealed class CloudPromoteRequest
{
    public string requestId, configVersion;
    public PromotionTarget target;
    public long expectedRevision;
    internal bool IsValid => Guid.TryParseExact(requestId, "D", out var id) && id != Guid.Empty && target?.IsValid == true &&
        configVersion?.Length == 64 && expectedRevision > 0 && expectedRevision < long.MaxValue;
}
internal sealed class CloudPromoteReply { public CloudPromoteOperation operation; public CloudSaveSnapshot state; public bool replayed; }
internal sealed class CloudPromoteOperation
{
    public string requestId, configVersion;
    public PromotionTarget target;
    public long committedRevision;
    public int level, oldRank, rank, oldExp, exp, goldCost;
    public PromotionMaterial[] materials;
}
