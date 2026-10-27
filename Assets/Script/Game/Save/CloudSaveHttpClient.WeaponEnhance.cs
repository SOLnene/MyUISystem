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
    internal async UniTask<CloudWeaponEnhanceConfig> LoadWeaponEnhanceConfigAsync(string playerId, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Get, playerId, null, token, new Uri(saveUri, "/v1/weapons/enhance/config"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        var config = JsonConvert.DeserializeObject<CloudWeaponEnhanceConfig>(body);
        if (config?.rules == null || config.configVersion?.Length != 64) throw new InvalidDataException("Invalid enhancement configuration.");
        config.rules.Validate(); return config;
    }

    internal async UniTask<CloudWeaponEnhanceReply> EnhanceWeaponAsync(string playerId, CloudWeaponEnhanceRequest request, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Post, playerId, JsonConvert.SerializeObject(request), token,
            new Uri(saveUri, "/v1/weapons/enhance"));
        string body = await response.Content.ReadAsStringAsync().AsUniTask(); EnsureSuccess(response, body);
        var reply = JsonConvert.DeserializeObject<CloudWeaponEnhanceReply>(body);
        var operation = reply?.operation;
        if (operation == null || reply.state == null || operation.requestId != request.requestId ||
            operation.weaponInstanceId != request.weaponInstanceId || operation.materialInstanceIds == null ||
            !operation.materialInstanceIds.SequenceEqual(request.materialInstanceIds) || operation.configVersion != request.configVersion ||
            operation.committedRevision != checked(request.expectedRevision + 1) || reply.state.revision < operation.committedRevision ||
            operation.level < operation.oldLevel || operation.exp < 0 || operation.gainedExp <= 0 || operation.goldCost < 0 ||
            string.IsNullOrWhiteSpace(reply.state.saveJson)) throw new InvalidDataException("Invalid weapon enhancement receipt.");
        return reply;
    }
}

[Serializable]
internal sealed class CloudWeaponEnhanceConfig
{
    public string configVersion;
    public WeaponEnhanceRules rules;
}
[Serializable]
internal sealed class CloudWeaponEnhanceRequest
{
    public string requestId;
    public long weaponInstanceId;
    public long[] materialInstanceIds;
    public long expectedRevision;
    public string configVersion;
    internal bool IsValid => Guid.TryParseExact(requestId, "D", out var id) && id != Guid.Empty && weaponInstanceId > 0 &&
        expectedRevision > 0 && expectedRevision < long.MaxValue && configVersion?.Length == 64 &&
        materialInstanceIds != null && materialInstanceIds.Length > 0 && materialInstanceIds.Length <= 40 &&
        materialInstanceIds.All(i => i > 0 && i != weaponInstanceId) && materialInstanceIds.Distinct().Count() == materialInstanceIds.Length;
}
[Serializable]
internal sealed class CloudWeaponEnhanceReply
{
    public CloudWeaponEnhanceOperation operation;
    public CloudSaveSnapshot state;
    public bool replayed;
}
[Serializable]
internal sealed class CloudWeaponEnhanceOperation
{
    public string requestId;
    public long weaponInstanceId;
    public long[] materialInstanceIds;
    public string configVersion;
    public long committedRevision;
    public int oldLevel, oldExp, level, exp, gainedExp, goldCost;
}
