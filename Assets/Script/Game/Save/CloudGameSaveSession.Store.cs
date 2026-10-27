using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

internal sealed partial class CloudGameSaveSession
{
    CloudStoreRequest pendingStore;
    internal bool HasPendingStore => pendingStore != null;
    internal CloudStoreRequest PendingStore => pendingStore;
    string StoreJournalPath => Path.Combine(Application.persistentDataPath,
        "store-request-" + client.GachaJournalKey(PlayerId) + ".json");

    internal void RequireNoPendingStore()
    {
        if (HasPendingStore) throw new InvalidOperationException("购买结果尚未确认，请返回商城重试或重新登录恢复。");
    }

    internal UniTask<CloudStoreSnapshot> LoadStoreAsync(CancellationToken cancellationToken)
    {
        RequireOwner();
        if (HasConflict || !IsReady) throw new InvalidOperationException("云端存档尚未就绪，请重新登录。");
        return client.LoadStoreAsync(PlayerId, cancellationToken);
    }

    internal async UniTask<CloudStoreReply> PurchaseStoreAsync(int storeItemId, int count, string configVersion,
        CancellationToken cancellationToken)
    {
        RequireNoPendingEnhance();
        RequireOwner();
        if (HasConflict || !IsReady || IsSaving || HasPendingSave || HasPendingGacha)
            throw new InvalidOperationException("云端存档尚未就绪，请先确认上一次操作。");
        if (pendingStore == null)
        {
            var request = new CloudStoreRequest { requestId = Guid.NewGuid().ToString("D"),
                storeItemId = storeItemId, count = count, expectedRevision = revision, configVersion = configVersion };
            if (!request.IsValid) throw new InvalidOperationException("购买参数无效，请刷新商店。");
            string temporary = StoreJournalPath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            // Persist the original request before sending; a lost response must not become a second purchase.
            if (File.Exists(StoreJournalPath)) File.Replace(temporary, StoreJournalPath, null);
            else File.Move(temporary, StoreJournalPath);
            pendingStore = request;
        }
        else if (pendingStore.storeItemId != storeItemId || pendingStore.count != count)
            throw new InvalidOperationException("上一次购买尚未确认，请先恢复购买结果。");
        return await ResolveStoreAsync(cancellationToken);
    }

    async UniTask<CloudStoreReply> ResolveStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var reply = await client.PurchaseStoreAsync(PlayerId, pendingStore, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RequireOwner();
            return reply;
        }
        catch (CloudSaveServerException error) when (error.StatusCode == 400 || error.StatusCode == 409)
        {
            if (error.Code == "save_conflict" || error.Code == "request_id_reused") HasConflict = true;
            ClearStoreJournal();
            throw;
        }
    }

    internal void AcceptStore(CloudStoreReply reply)
    {
        RequireOwner();
        if (pendingStore == null || pendingStore.requestId != reply.operation.requestId)
            throw new InvalidOperationException("购买回执不属于当前待确认请求。");
        Json = reply.state.saveJson; revision = reply.state.revision;
        pendingJson = null; inFlightRequest = null;
        ClearStoreJournal();
    }

    async UniTask RecoverStoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoreJournalPath)) return;
        pendingStore = JsonConvert.DeserializeObject<CloudStoreRequest>(File.ReadAllText(StoreJournalPath, Encoding.UTF8));
        if (pendingStore == null || !pendingStore.IsValid)
            throw new InvalidDataException("未确认购买记录损坏，已停止发起新购买。");
        try { AcceptStore(await ResolveStoreAsync(cancellationToken)); }
        catch (CloudSaveServerException error) when (error.Code == "save_conflict")
        {
            var snapshot = await client.LoadAsync(PlayerId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); RequireOwner();
            Json = snapshot?.saveJson; revision = snapshot?.revision ?? 0; HasConflict = false;
        }
        catch (CloudSaveServerException error) when (!HasConflict && (error.StatusCode == 400 || error.StatusCode == 409))
        {
            // Definitive rejection did not commit a purchase; keep the freshly loaded server state.
        }
    }

    void ClearStoreJournal()
    {
        File.Delete(StoreJournalPath); pendingStore = null;
    }
}
