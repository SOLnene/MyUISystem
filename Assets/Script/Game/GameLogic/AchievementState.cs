using System;
using UniRx;

internal sealed class AchievementState : IDisposable
{
    readonly CompositeDisposable disposable = new();

    internal AchievementDefinition Definition { get; }
    internal ItemDefinition RewardItem { get; }
    internal IReadOnlyReactiveProperty<int> CurrentProgress { get; }
    internal IReadOnlyReactiveProperty<bool> IsCompleted { get; }
    internal IReadOnlyReactiveProperty<bool> IsClaimed { get; }
    internal IReadOnlyReactiveProperty<bool> CanClaim { get; }

    internal AchievementState(
        AchievementDefinition definition,
        ItemDefinition rewardItem)
    {
        Definition = definition;
        RewardItem = rewardItem;
        CurrentProgress = AchievementProgressService.Instance
            .ObserveProgress(definition.progressKey)
            .Select(progress => Math.Min(definition.target, progress))
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        IsCompleted = CurrentProgress
            .Select(progress => progress >= definition.target)
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        IsClaimed = AchievementProgressService.Instance
            .ObserveClaimed(definition.id)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        CanClaim = IsCompleted
            .CombineLatest(
                IsClaimed,
                (isCompleted, isClaimed) => isCompleted && !isClaimed)
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
