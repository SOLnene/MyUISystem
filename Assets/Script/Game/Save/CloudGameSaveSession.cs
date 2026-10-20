using System;
using System.Threading;
using System.IO;
using Cysharp.Threading.Tasks;

internal sealed partial class CloudGameSaveSession
{
    internal const string SaveKey = "game_save";
    const string SourcePrefix = "cloud-account:";
    internal static CloudGameSaveSession Current { get; set; }

    internal string PlayerId { get; }
    internal string Username { get; }
    internal string Source { get; }
    internal string Json { get; private set; }
    internal bool IsReady { get; set; }
    internal bool IsSaving { get; private set; }
    internal bool HasConflict { get; private set; }
    internal bool HasPendingSave => pendingJson != null;
    readonly CloudSaveHttpClient client;
    long revision;
    string pendingJson;
    CloudSaveWriteRequest inFlightRequest;

    CloudGameSaveSession(string playerId, string username, string json, long version, CloudSaveHttpClient transport)
    {
        PlayerId = playerId;
        // 重读同一账号也要创建新候选，避免 GameContext 复用存在冲突的旧会话。
        Source = SourcePrefix + playerId + ":" + Guid.NewGuid().ToString("N");
        Username = username;
        Json = json;
        revision = version;
        client = transport;
    }

    internal static bool IsCloudSource(string source)
    {
        // 这是内存会话标识，不能作为文件目录使用。
        return source != null && source.StartsWith(SourcePrefix, StringComparison.Ordinal);
    }

    internal static CloudGameSaveSession RequireSession(string source)
    {
        var session = Current;
        if (session == null || session.Source != source)
            throw new InvalidOperationException("云存档会话与当前游戏身份不一致。");
        session.RequireOwner();
        return session;
    }

    void RequireOwner()
    {
        var authentication = AccountSession.Current;
        if (!authentication.IsAuthorized || authentication.PlayerId != PlayerId)
            throw new InvalidOperationException("登录身份已变化，请重新登录后读取云存档。");
    }

    internal static UniTask<CloudGameSaveSession> LoadAsync(string username, CancellationToken cancellationToken = default)
    {
        return LoadAsync(username, CloudSaveHttpClient.Default, cancellationToken);
    }

    internal static async UniTask<CloudGameSaveSession> LoadAsync(string username, CloudSaveHttpClient client,
        CancellationToken cancellationToken = default)
    {
        var authentication = AccountSession.Current;
        if (!authentication.IsAuthorized)
            throw new InvalidOperationException("请先完成账号登录。");
        string playerId = authentication.PlayerId;
        var snapshot = await client.LoadAsync(playerId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var session = new CloudGameSaveSession(playerId, username, snapshot?.saveJson,
            snapshot?.revision ?? 0, client);
        session.RequireOwner();
        await session.RecoverGachaAsync(cancellationToken);
        return session;
    }

    internal void QueueSave(string json)
    {
        RequireOwner();
        if (HasConflict)
            throw new InvalidOperationException("云存档已被其他设备更新，请重新启动并登录。");
        if (HasPendingGacha)
            throw new InvalidOperationException("抽卡结果尚未确认，请先恢复抽卡结果再保存。");
        pendingJson = json;
    }

    internal async UniTask SavePendingAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving)
            throw new InvalidOperationException("已有云存档上传正在执行。");
        RequireOwner();
        if (HasConflict)
            throw new InvalidOperationException("云存档存在版本冲突，停止自动覆盖。");
        if (HasPendingGacha)
            throw new InvalidOperationException("抽卡结果尚未确认，请先重试抽卡或重新登录。");
        if (pendingJson == null)
            return;
        IsSaving = true;
        // 请求失败可能只是响应丢失；新快照不能替换尚未确认请求的编号、版本和内容。
        if (inFlightRequest == null)
            inFlightRequest = new CloudSaveWriteRequest(revision, pendingJson);
        string snapshot = inFlightRequest.saveJson;
        try
        {
            // 首次创建用版本 0；创建与更新均由服务端事务检查，重试沿用原请求。
            var receipt = await client.SaveAsync(PlayerId, inFlightRequest, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RequireOwner();
            revision = receipt.revision;
            Json = snapshot;
            inFlightRequest = null;
            // 上传期间的新快照必须保留，不能被旧响应清掉。
            if (pendingJson == snapshot)
                pendingJson = null;
        }
        catch (CloudSaveServerException exception) when (exception.StatusCode == 409)
        {
            HasConflict = true;
            throw;
        }
        finally
        {
            IsSaving = false;
        }
    }
}

