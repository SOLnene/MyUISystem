using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed partial class GameSaveCoordinator
{
    uint? gachaDirtyRevision;

    internal async UniTask<GachaResult> DrawGachaAsync(GachaService service, string key, int count,
        CancellationToken cancellationToken = default)
    {
        while (isFlushing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield();
        }
        var context = GameContext.Instance;
        string source = context.ActiveSaveDirectory;
        var session = CloudGameSaveSession.RequireSession(source);
        if (!ReferenceEquals(service, context.GachaService) || !session.IsReady || session.HasConflict)
            throw new InvalidOperationException("抽卡会话尚未就绪，请重新登录。");
        if (!session.HasPendingGacha)
        {
            if (string.IsNullOrEmpty(session.Json)) MarkDirty();
            // A previous upload may only acknowledge an older snapshot; drain it before drawing.
            while (dirty || session.HasPendingSave)
                if (!await FlushCloudAsync(cancellationToken) && LastCloudError != null)
                    throw LastCloudError;
            gachaDirtyRevision = dirtyRevision;
        }
        if (gachaDirtyRevision != dirtyRevision)
            throw new InvalidOperationException("未确认抽卡期间本地进度发生变化，请退出并重新登录恢复。");

        isFlushing = true;
        try
        {
            CloudGameSaveSession.RequireSession(source);
            var reply = await session.DrawAsync(key, count, service.ConfigurationVersion, cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);
            if (!ReferenceEquals(service, context.GachaService) ||
                CloudGameSaveSession.RequireSession(source) != session)
                throw new InvalidOperationException("抽卡响应不属于当前游戏会话。");
            if (gachaDirtyRevision != dirtyRevision)
            {
                session.RejectGachaApplication();
                throw new InvalidOperationException("抽卡期间本地进度发生变化，请退出并重新登录恢复。");
            }
            try
            {
                var result = CloudGachaResultApplier.Apply(reply, context.InventoryRepository,
                    context.CharacterRepository, service, out int progress);
                var achievements = AchievementProgressService.Instance;
                achievements.AddProgress(AchievementProgressKeys.GachaDraw,
                    Math.Max(0, progress - achievements.GetProgress(AchievementProgressKeys.GachaDraw)));
                session.AcceptGacha(reply);
                gachaDirtyRevision = null;
                LastCloudError = null;
                return result;
            }
            catch
            {
                // Do not upload partially applied data; login recovery imports the complete server snapshot.
                session.RejectGachaApplication();
                throw;
            }
        }
        catch (Exception exception)
        {
            LastCloudError = exception;
            throw;
        }
        finally
        {
            isFlushing = false;
            saveAt = Time.realtimeSinceStartup + SaveDelay;
        }
    }
}
