using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;

internal enum ProgressionKind { WeaponEnhance = 1, WeaponRefine = 2, CharacterEnhance = 3, Promote = 4 }
internal enum ProgressionRunState { Idle, Running, AwaitingConfirmation, NeedsSync }

// The lifecycle knows no weapon or character fields; each operation owns its prediction and typed receipt.
internal abstract class ProgressionOperation
{
    internal readonly ProgressionKind Kind;
    internal readonly string TargetKey, RetryMessage, ChangedMessage;
    internal readonly ReactiveProperty<bool> Busy;
    internal uint DirtyRevision;
    internal bool Applied;

    protected ProgressionOperation(ProgressionKind kind, string targetKey, ReactiveProperty<bool> busy,
        string retryMessage, string changedMessage)
    { Kind = kind; TargetKey = targetKey; Busy = busy; RetryMessage = retryMessage; ChangedMessage = changedMessage; }

    internal abstract UniTask PrepareAsync(CloudGameSaveSession session, CancellationToken token);
    internal abstract void ApplyPrediction();
    internal abstract void NotifyPrediction();
    internal abstract UniTask ResolveAsync(CloudGameSaveSession session, CancellationToken token);
    internal abstract bool MatchesLocal();
    internal abstract void Restore();
    internal abstract void Confirm(CloudGameSaveSession session);
}
