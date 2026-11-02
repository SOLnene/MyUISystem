using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

internal sealed partial class CloudGameSaveSession
{
    internal CloudWeaponEnhanceConfig EnhanceConfig { get; private set; }
    internal CloudWeaponEnhanceRequest PendingEnhance => HasPendingEnhance ? (CloudWeaponEnhanceRequest)pendingProgression.Request : null;
    internal bool HasPendingEnhance => pendingProgression?.Kind == ProgressionKind.WeaponEnhance;
    string EnhanceJournalPath => Path.Combine(Application.persistentDataPath,
        "weapon-enhance-request-" + client.GachaJournalKey(PlayerId) + ".json");

    internal void RequireNoPendingEnhance()
    {
        // All existing wallet/inventory writers already enter this weapon-operation guard.
        RequireNoPendingRefine();
        if (HasPendingEnhance) throw new InvalidOperationException("强化结果尚未确认，请返回武器强化页重试或重新登录恢复。");
        RequireNoPendingProgression();
    }

    internal async UniTask LoadEnhanceConfigAsync(CancellationToken token)
    {
        RequireOwner();
        var config = await client.LoadWeaponEnhanceConfigAsync(PlayerId, token);
        await UniTask.SwitchToMainThread(token); RequireOwner();
        EnhanceConfig = config;
    }

    internal void BeginEnhancement(long weaponId, long[] materialIds)
    {
        RequireOwner(); RequireNoPendingStore(); RequireNoPendingEnhance();
        if (HasConflict || !IsReady || IsSaving || HasPendingSave || HasPendingGacha || EnhanceConfig == null)
            throw new InvalidOperationException("云端强化尚未就绪，请先完成上一次操作。");
        var request = new CloudWeaponEnhanceRequest { requestId = Guid.NewGuid().ToString("D"), weaponInstanceId = weaponId,
            materialInstanceIds = (long[])materialIds.Clone(), expectedRevision = revision, configVersion = EnhanceConfig.configVersion };
        if (!request.IsValid) throw new InvalidOperationException("强化材料无效，请重新选择。");
        // Journal first, predict second. A crash or lost reply always recovers this exact operation.
        BeginProgression(ProgressionKind.WeaponEnhance, request);
    }

    internal async UniTask<CloudWeaponEnhanceReply> ResolveEnhancementAsync(CancellationToken token)
    {
        RequireOwner();
        if (PendingEnhance == null) throw new InvalidOperationException("没有待确认的强化请求。");
        var request = PendingEnhance;
        return await ResolveProgressionAsync(ProgressionKind.WeaponEnhance, "enhance_request_not_found",
            ct => client.EnhanceWeaponAsync(PlayerId, request, ct),
            ct => client.FindWeaponEnhancementAsync(PlayerId, request, ct), token);
    }

    internal void AcceptEnhancement(CloudWeaponEnhanceReply reply)
    {
        RequireOwner();
        if (PendingEnhance?.requestId != reply.operation.requestId) throw new InvalidOperationException("强化回执不属于当前请求。");
        AcceptProgression(ProgressionKind.WeaponEnhance, reply.operation.requestId, reply.state);
    }

    static CloudWeaponEnhanceRequest ReadEnhancementRequest(string json)
    {
        var request = JsonConvert.DeserializeObject<CloudWeaponEnhanceRequest>(json);
        if (request == null || !request.IsValid) throw new InvalidDataException("未确认强化记录损坏，已停止发起新强化。");
        return request;
    }
}
