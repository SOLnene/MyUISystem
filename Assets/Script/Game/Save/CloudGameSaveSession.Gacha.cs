using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal sealed partial class CloudGameSaveSession
{
    CloudGachaRequest pendingGacha;
    internal bool HasPendingGacha => pendingGacha != null;
    string gachaJournalPath;
    string GachaJournalPath => gachaJournalPath ??= Path.Combine(Application.persistentDataPath,
        "gacha-request-" + client.GachaJournalKey(PlayerId) + ".json");

    internal async UniTask<CloudGachaReply> DrawAsync(string key, int count, long configVersion,
        CancellationToken cancellationToken)
    {
        RequireOwner();
        if (HasConflict || !IsReady || IsSaving || HasPendingSave)
            throw new InvalidOperationException("云端存档尚未就绪，请先完成保存或重新登录。");
        if (pendingGacha == null)
        {
            var request = new CloudGachaRequest
            {
                requestId = Guid.NewGuid().ToString("D"), gachaKey = key, count = count,
                configVersion = configVersion, expectedRevision = revision
            };
            if (!request.IsValid) throw new InvalidOperationException("抽卡参数或卡池版本无效，请刷新卡池。");
            WriteGachaJournal(GachaJournalPath, request);
            pendingGacha = request;
        }
        else if (pendingGacha.gachaKey != key || pendingGacha.count != count)
            throw new InvalidOperationException("上一次抽卡尚未确认，请回到原卡池重试相同次数，或退出后重新登录恢复。");
        return await ResolveGachaAsync(cancellationToken);
    }

    async UniTask<CloudGachaReply> ResolveGachaAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A missing receipt may race a committing POST; always resend the original ID and payload.
            var reply = await client.FindDrawAsync(PlayerId, pendingGacha, cancellationToken)
                ?? await client.DrawAsync(PlayerId, pendingGacha, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RequireOwner();
            return reply;
        }
        catch (CloudSaveServerException error) when (error.StatusCode == 400 || error.StatusCode == 409)
        {
            if (error.Code == "save_conflict" || error.Code == "request_id_reused")
                HasConflict = true;
            ClearGachaJournal();
            throw;
        }
    }

    async UniTask RecoverGachaAsync(CancellationToken cancellationToken)
    {
        pendingGacha = ReadGachaJournal(GachaJournalPath);
        if (pendingGacha == null) return;
        CloudGachaReply reply;
        try { reply = await ResolveGachaAsync(cancellationToken); }
        catch (CloudSaveServerException error) when (error.Code == "save_conflict")
        {
            // This stale journal was rejected definitively; reload rather than poisoning the new login.
            var current = await client.LoadAsync(PlayerId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RequireOwner();
            Json = current?.saveJson;
            revision = current?.revision ?? 0;
            HasConflict = false;
            return;
        }
        catch (CloudSaveServerException error) when (!HasConflict &&
            (error.StatusCode == 400 || error.StatusCode == 409))
        {
            // A definitive rejection did not grant anything; the already loaded snapshot remains valid.
            return;
        }
        // Login has not imported repositories yet, so recovery adopts the latest complete snapshot.
        AcceptGacha(reply);
    }

    internal void AcceptGacha(CloudGachaReply reply)
    {
        RequireOwner();
        if (pendingGacha == null || pendingGacha.requestId != reply.operation.requestId)
            throw new InvalidOperationException("抽卡回执不属于当前待确认请求。");
        // Keep the journal until local application succeeds; a crash recovers from the server on login.
        Json = reply.state.saveJson;
        revision = reply.state.revision;
        pendingJson = null;
        inFlightRequest = null;
        ClearGachaJournal();
    }

    internal void RejectGachaApplication()
    {
        HasConflict = true;
    }

    void ClearGachaJournal()
    {
        File.Delete(GachaJournalPath);
        pendingGacha = null;
    }

    internal static CloudGachaRequest ReadGachaJournal(string path)
    {
        if (!File.Exists(path)) return null;
        var request = JsonUtility.FromJson<CloudGachaRequest>(File.ReadAllText(path, Encoding.UTF8));
        if (request == null || !request.IsValid)
            throw new InvalidDataException("未确认抽卡记录损坏，已停止发起新抽卡。");
        return request;
    }

    internal static void WriteGachaJournal(string path, CloudGachaRequest request)
    {
        if (!request.IsValid) throw new InvalidDataException("Invalid pending gacha request.");
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        // Never send before the request is durable; the final file is replaced atomically.
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }
}
