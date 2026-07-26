using System;

public sealed class AchievementItemViewModel : IDisposable
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string IconAddress { get; }
    public int CurrentProgress { get; }
    public int TargetProgress { get; }
    public string ProgressText => $"{CurrentProgress}/{TargetProgress}";
    public string ButtonText => "未完成";
    public bool CanClaim => false;
    public ItemSlotViewModel RewardSlot { get; }

    public AchievementItemViewModel(
        AchievementDefinition definition,
        ItemDefinition rewardItem,
        int rewardAmount)
    {
        Id = definition.id;
        Title = definition.title;
        Description = definition.description;
        IconAddress = definition.iconAddress;
        CurrentProgress = 0;
        TargetProgress = Math.Max(1, definition.target);

        RewardSlot = new ItemSlotViewModel();
        RewardSlot.isEmpty.Value = false;
        RewardSlot.iconPath.Value = rewardItem.iconPath;
        RewardSlot.count.Value = rewardAmount.ToString();
        RewardSlot.star.Value = rewardItem.stars;
        RewardSlot.color.Value = RarityConfig.GetColor(rewardItem.itemRarity);
    }

    public void Dispose()
    {
        RewardSlot.Dispose();
    }
}
