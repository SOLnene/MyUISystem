using System.Collections.Generic;

public sealed class StoreConfigDatabase
{
    readonly List<StoreConfigItemData> allItems;
    readonly Dictionary<int, StoreConfigItemData> itemsById = new();

    public IReadOnlyList<StoreConfigItemData> Items => allItems;

    public StoreConfigDatabase(IReadOnlyList<StoreConfigItemData> items)
    {
        allItems = new List<StoreConfigItemData>(items);
        allItems.Sort(CompareItems);
        foreach (StoreConfigItemData item in allItems)
        {
            itemsById.Add(item.StoreItemId, item);
        }
    }

    public bool TryGetItem(int storeItemId, out StoreConfigItemData item)
    {
        return itemsById.TryGetValue(storeItemId, out item);
    }

    static int CompareItems(StoreConfigItemData left, StoreConfigItemData right)
    {
        int categoryComparison = left.Category.CompareTo(right.Category);
        return categoryComparison != 0
            ? categoryComparison
            : left.Order.CompareTo(right.Order);
    }
}
