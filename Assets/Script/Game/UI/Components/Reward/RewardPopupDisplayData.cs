using System.Collections.Generic;

public readonly struct RewardItemDisplayData
{
    public readonly string ItemName;
    public readonly string IconAddress;
    public readonly int Amount;
    public readonly ItemRarity Rarity;
    public readonly int Star;

    public RewardItemDisplayData(
        string itemName,
        string iconAddress,
        int amount,
        ItemRarity rarity,
        int star)
    {
        ItemName = itemName;
        IconAddress = iconAddress;
        Amount = amount;
        Rarity = rarity;
        Star = star;
    }
}

public sealed class RewardPopupDisplayData
{
    public string Title { get; }
    public string SectionTitle { get; }
    public IReadOnlyList<RewardItemDisplayData> Items { get; }

    public RewardPopupDisplayData(
        IReadOnlyList<RewardItemDisplayData> items,
        string title = "获得",
        string sectionTitle = "额外获得")
    {
        Items = items;
        Title = title;
        SectionTitle = sectionTitle;
    }
}
