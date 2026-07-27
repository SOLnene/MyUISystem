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
    public IReadOnlyReactiveProperty<bool> CanClaim => IsCompleted;
    public ItemSlotViewModel RewardSlot { get; }

    readonly CompositeDisposable disposable = new();

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

        RewardSlot = new ItemSlotViewModel();
        RewardSlot.isEmpty.Value = false;
        RewardSlot.iconPath.Value = rewardItem.iconPath;
        RewardSlot.count.Value = rewardAmount.ToString();
        RewardSlot.star.Value = rewardItem.stars;
        RewardSlot.color.Value = RarityConfig.GetColor(rewardItem.itemRarity);
    }

    public void Dispose()
    {
        disposable.Dispose();
        RewardSlot.Dispose();
    }
}
