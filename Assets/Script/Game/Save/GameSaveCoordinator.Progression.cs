using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed partial class GameSaveCoordinator
{
    // Pending/sync state belongs to the current session, so a re-login cannot inherit an old session's status.
    internal ProgressionRunState ProgressionState => IsProgressionRunning ? ProgressionRunState.Running :
        CloudGameSaveSession.Current?.HasConflict == true ? ProgressionRunState.NeedsSync :
        CloudGameSaveSession.Current?.HasPendingProgression == true ? ProgressionRunState.AwaitingConfirmation : ProgressionRunState.Idle;
    ProgressionOperation pendingProgressionOperation;
    string progressionSource;
    bool IsProgressionRunning;

    async UniTask RunProgressionAsync(ProgressionOperation operation, CancellationToken token)
    {
        if (IsProgressionRunning) throw new InvalidOperationException("养成操作正在处理中。");
        IsProgressionRunning = true;
        string source = GameContext.Instance.ActiveSaveDirectory;
        CloudGameSaveSession session = null;
        bool ownsFlush = false;
        try
        {
            operation.Busy.Value = true;
            while (isFlushing) { token.ThrowIfCancellationRequested(); await UniTask.Yield(); }
            session = CloudGameSaveSession.RequireSession(source);
            session.RequireNoPendingStore();
            if (!session.IsReady || session.HasConflict || session.HasPendingGacha)
                throw new InvalidOperationException("云端养成尚未就绪，请先同步上一次操作。");
            if (session.HasPendingProgression)
            {
                var pending = pendingProgressionOperation;
                if (pending == null || progressionSource != source || pending.Kind != operation.Kind || pending.TargetKey != operation.TargetKey)
                    throw new InvalidOperationException(operation.RetryMessage);
                operation = pending;
            }
            else
            {
                while (dirty || session.HasPendingSave)
                    if (!await FlushCloudAsync(token) && LastCloudError != null) throw LastCloudError;
                if (CloudGameSaveSession.RequireSession(source) != session)
                    throw new InvalidOperationException(operation.ChangedMessage);
                isFlushing = true; ownsFlush = true;
                await operation.PrepareAsync(session, token);
                if (CloudGameSaveSession.RequireSession(source) != session)
                    throw new InvalidOperationException(operation.ChangedMessage);
                pendingProgressionOperation = operation; progressionSource = source;
                try { operation.Applied = true; operation.ApplyPrediction(); }
                finally
                {
                    // Only this synchronous prediction is covered by its journal. Later dirty revisions require a full sync.
                    dirty = false; operation.DirtyRevision = dirtyRevision;
                }
                // A view observer failing must not turn a durable operation into an unsubmitted or rolled-back request.
                try { operation.NotifyPrediction(); }
                catch (Exception error) { Debug.LogException(error); }
            }
            isFlushing = true; ownsFlush = true;
            if (operation.DirtyRevision != dirtyRevision || !operation.MatchesLocal())
            {
                session.MarkProgressionNeedsSync();
                throw new InvalidOperationException(operation.ChangedMessage);
            }
            await operation.ResolveAsync(session, token);
            await UniTask.SwitchToMainThread(token);
            if (CloudGameSaveSession.RequireSession(source) != session || operation.DirtyRevision != dirtyRevision || !operation.MatchesLocal())
            {
                session.MarkProgressionNeedsSync();
                throw new InvalidOperationException(operation.ChangedMessage);
            }
            try
            {
                operation.Confirm(session); pendingProgressionOperation = null;
                dirty = false; LastCloudError = null;
            }
            catch { session.MarkProgressionNeedsSync(); throw; }
        }
        catch (Exception error)
        {
            await UniTask.SwitchToMainThread();
            if (session != null && !session.HasPendingProgression && pendingProgressionOperation != null)
            {
                var pending = pendingProgressionOperation;
                if (CloudGameSaveSession.Current == session && pending.DirtyRevision == dirtyRevision && pending.MatchesLocal())
                { pending.Restore(); dirty = false; }
                else if (pending.Applied) session.MarkProgressionNeedsSync();
                pendingProgressionOperation = null;
            }
            LastCloudError = error; throw;
        }
        finally
        {
            if (ownsFlush) isFlushing = false;
            saveAt = Time.realtimeSinceStartup + SaveDelay;
            IsProgressionRunning = false;
            operation.Busy.Value = false;
        }
    }
}
