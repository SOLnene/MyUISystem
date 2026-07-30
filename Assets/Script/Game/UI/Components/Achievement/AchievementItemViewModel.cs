using System;
using UniRx;

public sealed class AchievementItemViewModel : IDisposable
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string IconAddress { get; }
    public int TargetProgress { get; }
    public IReadOnlyReactiveProperty<int> CurrentProgress { get; }
    public IReadOnlyReactiveProperty<string> ProgressText { get; }
    public IReadOnlyReactiveProperty<bool> IsCompleted { get; }
    public string ButtonText => "领取";
    public IReadOnlyReactiveProperty<bool> CanClaim { get; }
    public ItemSlotViewModel RewardSlot { get; }

    readonly CompositeDisposable disposable = new();
    readonly ItemDefinition rewardItem;
    readonly int rewardAmount;

    internal IReadOnlyReactiveProperty<bool> IsClaimed { get; }
    internal ReactiveCommand ClaimCommand { get; }

    public AchievementItemViewModel(
        AchievementDefinition definition,
        ItemDefinition rewardItem,
        int rewardAmount)
    {
        Id = definition.id;
        Title = definition.title;
        Description = definition.description;
        IconAddress = definition.iconAddress;
        TargetProgress = Math.Max(1, definition.target);
        this.rewardItem = rewardItem;
        this.rewardAmount = rewardAmount;
        CurrentProgress = AchievementProgressService.Instance
            .ObserveProgress(definition.progressKey)
            .Select(progress => Math.Min(TargetProgress, progress))
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        ProgressText = CurrentProgress
            .Select(progress => $"{progress}/{TargetProgress}")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        IsCompleted = CurrentProgress
            .Select(progress => progress >= TargetProgress)
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        IsClaimed = AchievementProgressService.Instance
            .ObserveClaimed(Id)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        CanClaim = IsCompleted
            .CombineLatest(
                IsClaimed,
                (isCompleted, isClaimed) => isCompleted && !isClaimed)
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        ClaimCommand = CanClaim
            .ToReactiveCommand()
            .AddTo(disposable);
        ClaimCommand
            .Subscribe(_ => ClaimReward())
            .AddTo(disposable);

        RewardSlot = new ItemSlotViewModel();
        RewardSlot.isEmpty.Value = false;
        RewardSlot.iconPath.Value = rewardItem.iconPath;
        RewardSlot.count.Value = rewardAmount.ToString();
        RewardSlot.star.Value = rewardItem.stars;
        RewardSlot.color.Value = RarityConfig.GetColor(rewardItem.itemRarity);
    }

    void ClaimReward()
    {
        if (!IsCompleted.Value ||
            !AchievementProgressService.Instance.TryClaim(
                Id,
                () => RewardService.TryGrant(
                    rewardItem,
                    rewardAmount)))
        {
            return;
        }

        GameSaveCoordinator.Instance.MarkDirty();
    }

    public void Dispose()
    {
        disposable.Dispose();
        RewardSlot.Dispose();
    }
}
