using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class StoreViewModel
{
    const int PrimogemItemId = 201;
    const int MoraItemId = 202;
    const int GenesisCrystalItemId = 203;
    const int StarglitterItemId = 221;
    const int StardustItemId = 222;

    readonly StoreDatabase storeDatabase;
    readonly ItemDatabase itemDatabase;
    readonly StorePurchaseService purchaseService = new();

    public readonly ReactiveProperty<StoreCategory> CurrentTab = new(StoreCategory.Primogem);

    public StoreViewModel(StoreDatabase storeDatabase, ItemDatabase itemDatabase)
    {
        this.storeDatabase = storeDatabase;
        this.itemDatabase = itemDatabase;
    }

    public IReadOnlyList<StoreItemViewData> CreateItems()
    {
        return CreateItems(CurrentTab.Value);
    }

    public IReadOnlyList<StoreItemViewData> CreateItems(StoreCategory category)
    {
        var items = new List<StoreItemViewData>();
        if (storeDatabase == null || itemDatabase == null)
        {
            Debug.LogWarning("StoreViewModel cannot load items because database is not ready.");
            return items;
        }

        foreach (StoreItemDefinition storeItem in storeDatabase.Items)
        {
            if (!MatchesCategory(storeItem, category))
            {
                continue;
            }

            ItemDefinition itemDefinition = itemDatabase.GetItemByID(storeItem.ItemId);
            if (itemDefinition == null)
            {
                Debug.LogWarning($"Store item references missing item id: {storeItem.ItemId}");
                continue;
            }

            Color rarityColor = RarityConfig.GetColor(itemDefinition.itemRarity);
            int beforeValue = CalculateBeforeValue(storeItem);
            ItemDefinition costDefinition = itemDatabase.GetItemByID(storeItem.CostItemId);
            if (costDefinition == null)
            {
                Debug.LogWarning($"Store item references missing cost item id: {storeItem.CostItemId}");
            }

            items.Add(new StoreItemViewData(
                storeItem.StoreItemId,
                storeItem.ItemId.ToString(),
                GetDisplayName(itemDefinition, storeItem.Count),
                itemDefinition.iconPath,
                costDefinition?.iconPath,
                storeItem.Price,
                rarityColor,
                beforeValue: beforeValue,
                discountPercent: storeItem.DiscountPercent,
                hasBeforeValue: beforeValue > 0,
                hasDiscount: storeItem.HasDiscount));
        }

        return items;
    }

    public void SetTab(StoreCategory category)
    {
        CurrentTab.Value = category;
    }

    public IReadOnlyList<int> GetVisibleCurrencyItemIds(StoreCategory category)
    {
        return category switch
        {
            StoreCategory.Primogem => new[] { PrimogemItemId },
            StoreCategory.StarglitterStardust => new[] { StarglitterItemId, StardustItemId },
            StoreCategory.Mora => new[] { MoraItemId },
            StoreCategory.GenesisCrystal => new[] { GenesisCrystalItemId },
            _ => new[] { PrimogemItemId },
        };
    }

    public bool TryCreatePurchasePopupData(int storeItemId, out PurchasePopupViewData popupData)
    {
        popupData = default;
        if (storeDatabase == null || itemDatabase == null)
        {
            Debug.LogWarning("StoreViewModel cannot create purchase popup data because database is not ready.");
            return false;
        }

        StoreItemDefinition storeItem = FindStoreItem(storeItemId);
        if (storeItem == null)
        {
            Debug.LogWarning($"Cannot find store item id: {storeItemId}");
            return false;
        }

        ItemDefinition itemDefinition = itemDatabase.GetItemByID(storeItem.ItemId);
        if (itemDefinition == null)
        {
            Debug.LogWarning($"Store item references missing item id: {storeItem.ItemId}");
            return false;
        }

        ItemDefinition costDefinition = itemDatabase.GetItemByID(storeItem.CostItemId);
        if (costDefinition == null)
        {
            Debug.LogWarning($"Store item references missing cost item id: {storeItem.CostItemId}");
        }

        StorePurchasePreview preview = purchaseService.CreatePreview(storeItem);
        popupData = new PurchasePopupViewData(
            storeItem.StoreItemId,
            itemDefinition,
            costDefinition?.iconPath,
            storeItem.Count,
            storeItem.Price,
            preview.MaxPurchaseCount);
        return true;
    }

    public bool TryPurchase(int storeItemId, int purchaseCount)
    {
        if (purchaseCount <= 0)
        {
            return false;
        }

        if (storeDatabase == null || itemDatabase == null || GameContext.Instance.InventoryRepository == null)
        {
            Debug.LogWarning("购买失败：数据未初始化");
            return false;
        }

        StoreItemDefinition storeItem = FindStoreItem(storeItemId);
        if (storeItem == null)
        {
            Debug.LogWarning($"购买失败：找不到商品 {storeItemId}");
            return false;
        }

        ItemDefinition itemDefinition = itemDatabase.GetItemByID(storeItem.ItemId);
        if (itemDefinition == null)
        {
            Debug.LogWarning($"购买失败：找不到物品 {storeItem.ItemId}");
            return false;
        }

        return purchaseService.TryPurchase(storeItem, itemDefinition, purchaseCount);
    }

    static string GetDisplayName(ItemDefinition itemDefinition, int count)
    {
        return count > 1 ? $"{itemDefinition.itemName}x{count}" : itemDefinition.itemName;
    }

    StoreItemDefinition FindStoreItem(int storeItemId)
    {
        foreach (StoreItemDefinition storeItem in storeDatabase.Items)
        {
            if (storeItem.StoreItemId == storeItemId)
            {
                return storeItem;
            }
        }

        return null;
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

    static bool MatchesCategory(StoreItemDefinition storeItem, StoreCategory category)
    {
        return storeItem.Category == category;
    }
}
