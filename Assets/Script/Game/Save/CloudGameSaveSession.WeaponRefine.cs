using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

internal sealed partial class CloudGameSaveSession
{
    internal CloudWeaponRefineConfig RefineConfig { get; private set; }
    internal CloudWeaponRefineRequest PendingRefine => HasPendingRefine ? (CloudWeaponRefineRequest)pendingProgression.Request : null;
    internal bool HasPendingRefine => pendingProgression?.Kind == ProgressionKind.WeaponRefine;
    string RefineJournalPath => Path.Combine(Application.persistentDataPath,
        "weapon-refine-request-" + client.GachaJournalKey(PlayerId) + ".json");

    internal void RequireNoPendingRefine()
    {
        if (HasPendingRefine) throw new InvalidOperationException("精炼结果尚未确认，请返回原武器精炼页重试或重新登录恢复。");
    }

    internal async UniTask LoadRefineConfigAsync(CancellationToken token)
    {
        RequireOwner();
        var config = await client.LoadWeaponRefineConfigAsync(PlayerId, token);
        await UniTask.SwitchToMainThread(token); RequireOwner();
        RefineConfig = config;
    }

    internal void BeginRefinement(long weaponId, long[] materialIds)
    {
        RequireOwner(); RequireNoPendingStore(); RequireNoPendingEnhance();
        if (HasConflict || !IsReady || IsSaving || HasPendingSave || HasPendingGacha || RefineConfig == null)
            throw new InvalidOperationException("云端精炼尚未就绪，请先完成上一次操作。");
        var request = new CloudWeaponRefineRequest { requestId = Guid.NewGuid().ToString("D"), weaponInstanceId = weaponId,
            materialInstanceIds = (long[])materialIds.Clone(), expectedRevision = revision, configVersion = RefineConfig.configVersion };
        if (!request.IsValid) throw new InvalidOperationException("精炼材料无效，请重新选择。");
        // Persist the exact operation before predicting or sending; a retry must retain its original revision and materials.
        BeginProgression(ProgressionKind.WeaponRefine, request);
    }

    internal async UniTask<CloudWeaponRefineReply> ResolveRefinementAsync(CancellationToken token)
    {
        RequireOwner();
        if (PendingRefine == null) throw new InvalidOperationException("没有待确认的精炼请求。");
        var request = PendingRefine;
        return await ResolveProgressionAsync(ProgressionKind.WeaponRefine, "refine_request_not_found",
            ct => client.RefineWeaponAsync(PlayerId, request, ct),
            ct => client.FindWeaponRefinementAsync(PlayerId, request, ct), token);
    }

    internal void AcceptRefinement(CloudWeaponRefineReply reply)
    {
        RequireOwner();
        if (PendingRefine?.requestId != reply.operation.requestId) throw new InvalidOperationException("精炼回执不属于当前请求。");
        AcceptProgression(ProgressionKind.WeaponRefine, reply.operation.requestId, reply.state);
    }

    static CloudWeaponRefineRequest ReadRefinementRequest(string json)
    {
        var request = JsonConvert.DeserializeObject<CloudWeaponRefineRequest>(json);
        if (request == null || !request.IsValid) throw new InvalidDataException("未确认精炼记录损坏，已停止发起新精炼。");
        return request;
    }
}
