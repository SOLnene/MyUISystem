using System.Collections.Generic;
using UnityEngine;

public class StoreViewModel
{
    readonly StoreDatabase storeDatabase;
    readonly ItemDatabase itemDatabase;

    public StoreViewModel(StoreDatabase storeDatabase, ItemDatabase itemDatabase)
    {
        this.storeDatabase = storeDatabase;
        this.itemDatabase = itemDatabase;
    }

    public IReadOnlyList<StoreItemViewData> CreateItems()
    {
        var items = new List<StoreItemViewData>();
        if (storeDatabase == null || itemDatabase == null)
        {
            Debug.LogWarning("StoreViewModel cannot load items because database is not ready.");
            return items;
        }

        foreach (StoreItemDefinition storeItem in storeDatabase.Items)
        {
            ItemDefinition itemDefinition = itemDatabase.GetItemByID(storeItem.ItemId);
            if (itemDefinition == null)
            {
                Debug.LogWarning($"Store item references missing item id: {storeItem.ItemId}");
                continue;
            }

            Color rarityColor = RarityConfig.GetColor(itemDefinition.itemRarity);
            int beforeValue = CalculateBeforeValue(storeItem);

            items.Add(new StoreItemViewData(
                storeItem.ItemId.ToString(),
                GetDisplayName(itemDefinition, storeItem.Count),
                itemDefinition.iconPath,
                storeItem.Price,
                rarityColor,
                beforeValue: beforeValue,
                discountPercent: storeItem.DiscountPercent,
                hasBeforeValue: beforeValue > 0,
                hasDiscount: storeItem.HasDiscount));
        }

        return items;
    }

    static string GetDisplayName(ItemDefinition itemDefinition, int count)
    {
        return count > 1 ? $"{itemDefinition.itemName}x{count}" : itemDefinition.itemName;
    }

    static int CalculateBeforeValue(StoreItemDefinition storeItem)
    {
        if (!storeItem.HasDiscount || storeItem.DiscountPercent >= 100)
        {
            return 0;
        }

        float discountRate = (100 - storeItem.DiscountPercent) / 100f;
        return Mathf.CeilToInt(storeItem.Price / discountRate);
    }
}
