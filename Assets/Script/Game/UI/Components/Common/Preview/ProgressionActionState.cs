using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;

internal sealed class ProgressionActionState : IDisposable
{
    internal readonly ReactiveProperty<bool> Busy = new(false);
    internal bool Ready { get; set; }
    internal bool IsDisposed { get; private set; }
    readonly CancellationTokenSource loading = new();
    IReadOnlyReactiveProperty<bool> sending;
    IDisposable sendingSubscription;
    Action sync;
    bool loadingRules;

    internal void BindSending(IReadOnlyReactiveProperty<bool> sending, Action sync)
    {
        this.sending = sending;
        this.sync = sync;
        sendingSubscription = sending.Subscribe(_ => PublishState());
    }

    void PublishState()
    {
        if (IsDisposed) return;
        Busy.Value = loadingRules || sending.Value;
        sync();
    }

    internal async UniTask LoadRulesAsync(Func<CloudGameSaveSession, CancellationToken, UniTask<bool>> load,
        Action rulesChanged, Action<Exception> reportError)
    {
        if (IsDisposed || loadingRules) return;
        var token = loading.Token;
        loadingRules = true;
        PublishState();
        try
        {
            var session = CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory);
            bool ready = await load(session, token);
            if (IsDisposed) return;
            Ready = ready;
            rulesChanged();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Ready = false;
            if (!IsDisposed) reportError(error);
        }
        finally
        {
            loadingRules = false;
            PublishState();
        }
    }

    internal async UniTask ReportFailureAsync(Exception error, Func<bool> pending, string conflictMessage,
        string pendingMessage, string rejectedMessage, string configChangedCode,
        Action<string> report, Func<UniTask> reloadRules)
    {
        if (IsDisposed) return;
        // Resolve pending state only while the page is alive; its owner may already have been destroyed.
        // A lost response retains its journal and prediction; only an explicit rejection has rolled back.
        report(CloudGameSaveSession.Current?.HasConflict == true ? conflictMessage : pending() ? pendingMessage :
            error is CloudSaveServerException server ? rejectedMessage + server.Code : error.Message);
        if (error is CloudSaveServerException changed && changed.Code == configChangedCode)
            await reloadRules();
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        // Only config loading belongs to the page. Durable settlements keep running in the coordinator.
        IsDisposed = true;
        sendingSubscription?.Dispose();
        loading.Cancel();
        loading.Dispose();
        Busy.Dispose();
    }
}
