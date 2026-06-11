internal static class ItemCategoryDisplayName
{
    internal static string Get(ItemCategory category)
    {
        return category switch
        {
            ItemCategory.Equip => "武器",
            ItemCategory.Consumable => "消耗品",
            ItemCategory.Material => "材料",
            ItemCategory.QuestItem => "任务道具",
            ItemCategory.ExpBook => "经验素材",
            ItemCategory.All => "全部",
            _ => category.ToString()
        };
    }
}
