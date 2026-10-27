using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

internal sealed partial class CloudGameSaveSession
{
    internal CloudWeaponEnhanceConfig EnhanceConfig { get; private set; }
    internal CloudWeaponEnhanceRequest PendingEnhance { get; private set; }
    internal bool HasPendingEnhance => PendingEnhance != null;
    string EnhanceJournalPath => Path.Combine(Application.persistentDataPath,
        "weapon-enhance-request-" + client.GachaJournalKey(PlayerId) + ".json");

    internal void RequireNoPendingEnhance()
    {
        if (HasPendingEnhance) throw new InvalidOperationException("强化结果尚未确认，请返回武器强化页重试或重新登录恢复。");
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
            materialInstanceIds = materialIds, expectedRevision = revision, configVersion = EnhanceConfig.configVersion };
        if (!request.IsValid) throw new InvalidOperationException("强化材料无效，请重新选择。");
        string temporary = EnhanceJournalPath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
        // Journal first, predict second. A crash or lost reply always recovers this exact operation.
        if (File.Exists(EnhanceJournalPath)) File.Replace(temporary, EnhanceJournalPath, null);
        else File.Move(temporary, EnhanceJournalPath);
        PendingEnhance = request;
    }

    internal async UniTask<CloudWeaponEnhanceReply> ResolveEnhancementAsync(CancellationToken token)
    {
        RequireOwner();
        if (PendingEnhance == null) throw new InvalidOperationException("没有待确认的强化请求。");
        try
        {
            var reply = await client.EnhanceWeaponAsync(PlayerId, PendingEnhance, token);
            await UniTask.SwitchToMainThread(token); RequireOwner(); return reply;
        }
        catch (CloudSaveServerException error) when (error.StatusCode == 400 || error.StatusCode == 409)
        {
            if (error.Code == "save_conflict" || error.Code == "request_id_reused") HasConflict = true;
            ClearEnhanceJournal(); throw;
        }
    }

    internal void AcceptEnhancement(CloudWeaponEnhanceReply reply)
    {
        RequireOwner();
        if (PendingEnhance?.requestId != reply.operation.requestId) throw new InvalidOperationException("强化回执不属于当前请求。");
        Json = reply.state.saveJson; revision = reply.state.revision;
        pendingJson = null; inFlightRequest = null; ClearEnhanceJournal();
    }

    async UniTask RecoverEnhancementAsync(CancellationToken token)
    {
        if (!File.Exists(EnhanceJournalPath)) return;
        PendingEnhance = JsonConvert.DeserializeObject<CloudWeaponEnhanceRequest>(File.ReadAllText(EnhanceJournalPath, Encoding.UTF8));
        if (PendingEnhance == null || !PendingEnhance.IsValid) throw new InvalidDataException("未确认强化记录损坏，已停止发起新强化。");
        try { AcceptEnhancement(await ResolveEnhancementAsync(token)); }
        catch (CloudSaveServerException error) when (error.Code == "save_conflict")
        {
            var snapshot = await client.LoadAsync(PlayerId, token); RequireOwner();
            Json = snapshot?.saveJson; revision = snapshot?.revision ?? 0; HasConflict = false;
        }
        catch (CloudSaveServerException error) when (!HasConflict && (error.StatusCode == 400 || error.StatusCode == 409))
        {
            // Rejected before settlement; login will import the already loaded authoritative snapshot.
        }
    }
    void ClearEnhanceJournal()
    {
        File.Delete(EnhanceJournalPath); PendingEnhance = null;
    }
}
