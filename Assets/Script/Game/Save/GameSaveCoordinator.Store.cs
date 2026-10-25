using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed partial class GameSaveCoordinator
{
    uint? storeDirtyRevision;

    internal async UniTask PurchaseStoreAsync(StorePurchaseService service, int storeItemId, int count,
        string configVersion, CancellationToken cancellationToken)
    {
        while (isFlushing) { cancellationToken.ThrowIfCancellationRequested(); await UniTask.Yield(); }
        var context = GameContext.Instance;
        string source = context.ActiveSaveDirectory;
        var session = CloudGameSaveSession.RequireSession(source);
        if (!ReferenceEquals(service, context.StorePurchaseService) || !session.IsReady || session.HasConflict || session.HasPendingGacha)
            throw new InvalidOperationException("购买会话尚未就绪，请先确认上一次操作。");
        if (!session.HasPendingStore)
        {
            while (dirty || session.HasPendingSave)
                if (!await FlushCloudAsync(cancellationToken) && LastCloudError != null) throw LastCloudError;
            storeDirtyRevision = dirtyRevision;
        }
        if (storeDirtyRevision != dirtyRevision)
            throw new InvalidOperationException("购买未确认期间本地进度发生变化，请重新登录恢复。");
        isFlushing = true;
        try
        {
            var reply = await session.PurchaseStoreAsync(storeItemId, count, configVersion, cancellationToken);
            await UniTask.SwitchToMainThread(cancellationToken);
            if (!ReferenceEquals(service, context.StorePurchaseService) || CloudGameSaveSession.RequireSession(source) != session ||
                storeDirtyRevision != dirtyRevision)
            {
                session.RejectGachaApplication();
                throw new InvalidOperationException("购买期间游戏会话或本地进度发生变化，请重新登录恢复。");
            }
            try
            {
                CloudStoreResultApplier.Apply(reply);
                session.AcceptStore(reply);
                storeDirtyRevision = null; LastCloudError = null;
            }
            catch { session.RejectGachaApplication(); throw; }
        }
        catch (Exception error) { LastCloudError = error; throw; }
        finally { isFlushing = false; saveAt = Time.realtimeSinceStartup + SaveDelay; }
    }
}
