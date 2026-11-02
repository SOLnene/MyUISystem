using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Enhance;
using Newtonsoft.Json;

internal sealed partial class CloudSaveHttpClient
{
    internal async UniTask<CloudWeaponRefineConfig> LoadWeaponRefineConfigAsync(string playerId, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Get, playerId, null, token, new Uri(saveUri, "/v1/weapons/refine/config"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        var config = JsonConvert.DeserializeObject<CloudWeaponRefineConfig>(body);
        if (config?.rules == null || config.configVersion?.Length != 64) throw new InvalidDataException("Invalid refinement configuration.");
        config.rules.Validate(); return config;
    }

    internal UniTask<CloudWeaponRefineReply> RefineWeaponAsync(string playerId, CloudWeaponRefineRequest request, CancellationToken token)
        => SendRefinementAsync(HttpMethod.Post, playerId, request, token, "/v1/weapons/refine");

    internal UniTask<CloudWeaponRefineReply> FindWeaponRefinementAsync(string playerId, CloudWeaponRefineRequest request, CancellationToken token)
        => SendRefinementAsync(HttpMethod.Get, playerId, request, token, "/v1/weapons/refine/requests/" + request.requestId);

    async UniTask<CloudWeaponRefineReply> SendRefinementAsync(HttpMethod method, string playerId,
        CloudWeaponRefineRequest request, CancellationToken token, string path)
    {
        using var response = await SendAsync(method, playerId, method == HttpMethod.Post ? JsonConvert.SerializeObject(request) : null,
            token, new Uri(saveUri, path));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        var reply = JsonConvert.DeserializeObject<CloudWeaponRefineReply>(body);
        var op = reply?.operation;
        if (op == null || reply.state == null || op.requestId != request.requestId || op.weaponInstanceId != request.weaponInstanceId ||
            op.materialInstanceIds == null || !op.materialInstanceIds.SequenceEqual(request.materialInstanceIds) ||
            op.configVersion != request.configVersion || op.committedRevision != checked(request.expectedRevision + 1) ||
            reply.state.revision < op.committedRevision || op.oldRefinementLevel < 1 ||
            op.refinementLevel != op.oldRefinementLevel + request.materialInstanceIds.Length || op.refinementLevel > 5 ||
            op.goldCost < 0 || string.IsNullOrWhiteSpace(reply.state.saveJson))
            throw new InvalidDataException("Invalid weapon refinement receipt.");
        return reply;
    }
}

[Serializable]
internal sealed class CloudWeaponRefineConfig
{
    public string configVersion;
    public WeaponRefineRules rules;
}
[Serializable]
internal sealed class CloudWeaponRefineRequest
{
    public string requestId;
    public long weaponInstanceId;
    public long[] materialInstanceIds;
    public long expectedRevision;
    public string configVersion;
    internal bool IsValid => Guid.TryParseExact(requestId, "D", out var id) && id != Guid.Empty && weaponInstanceId > 0 &&
        expectedRevision > 0 && expectedRevision < long.MaxValue && configVersion?.Length == 64 && configVersion.All(Uri.IsHexDigit) &&
        materialInstanceIds != null && materialInstanceIds.Length > 0 && materialInstanceIds.Length <= 4 &&
        materialInstanceIds.All(i => i > 0 && i != weaponInstanceId) && materialInstanceIds.Distinct().Count() == materialInstanceIds.Length;
}
[Serializable]
internal sealed class CloudWeaponRefineReply
{
    public CloudWeaponRefineOperation operation;
    public CloudSaveSnapshot state;
    public bool replayed;
}
[Serializable]
internal sealed class CloudWeaponRefineOperation
{
    public string requestId;
    public long weaponInstanceId;
    public long[] materialInstanceIds;
    public string configVersion;
    public long committedRevision;
    public int oldRefinementLevel, refinementLevel, goldCost;
}
