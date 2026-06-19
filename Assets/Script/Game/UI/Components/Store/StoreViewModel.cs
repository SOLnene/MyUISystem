using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class StoreViewModel
{
    readonly StoreDatabase storeDatabase;
    readonly ItemDatabase itemDatabase;

    public readonly ReactiveProperty<StoreTabType> CurrentTab = new(StoreTabType.Gold);

    public StoreViewModel(StoreDatabase storeDatabase, ItemDatabase itemDatabase)
    {
        this.storeDatabase = storeDatabase;
        this.itemDatabase = itemDatabase;
    }

    public IReadOnlyList<StoreItemViewData> CreateItems()
    {
        return CreateItems(CurrentTab.Value);
    }

    public IReadOnlyList<StoreItemViewData> CreateItems(StoreTabType tab)
    {
        var items = new List<StoreItemViewData>();
        if (storeDatabase == null || itemDatabase == null)
        {
            Debug.LogWarning("StoreViewModel cannot load items because database is not ready.");
            return items;
        }

        foreach (StoreItemDefinition storeItem in storeDatabase.Items)
        {
            if (!MatchesTab(storeItem, tab))
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

    public void SetTab(StoreTabType tab)
    {
        CurrentTab.Value = tab;
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

        popupData = new PurchasePopupViewData(
            storeItem.StoreItemId,
            itemDefinition.itemName,
            itemDefinition.iconPath,
            costDefinition?.iconPath,
            storeItem.Count,
            storeItem.Price,
            1);
        return true;
    }

    public bool TryCreateInfoPanelItem(int storeItemId, out ItemViewModel itemViewModel)
    {
        itemViewModel = null;
        if (storeDatabase == null || itemDatabase == null)
        {
            Debug.LogWarning("StoreViewModel cannot create info panel item because database is not ready.");
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

        itemViewModel = new ItemViewModel(new InventoryItem(itemDefinition));
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

        int totalPrice = storeItem.Price * purchaseCount;
        if (!GameEconomy.Instance.TrySpendCurrency(storeItem.CostItemId, totalPrice))
        {
            return false;
        }

        AddPurchasedItem(itemDefinition, storeItem.Count * purchaseCount);
        return true;
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

    void AddPurchasedItem(ItemDefinition itemDefinition, int amount)
    {
        if (itemDefinition.category == ItemCategory.Currency)
        {
            GameEconomy.Instance.AddCurrency(itemDefinition.id, amount);
            return;
        }

        InventoryRepository inventoryRepository = GameContext.Instance.InventoryRepository;
        switch (itemDefinition.category)
        {
            case ItemCategory.Consumable:
                inventoryRepository.AddItem(new ConsumableItem(itemDefinition, amount));
                break;
            case ItemCategory.Material:
            case ItemCategory.ExpBook:
                inventoryRepository.AddItem(new MaterialItem(itemDefinition, amount));
                break;
            case ItemCategory.Equip:
                for (int i = 0; i < amount; i++)
                {
                    inventoryRepository.AddItem(new EquipItem(itemDefinition as EquipDefinition));
                }

                break;
            default:
                for (int i = 0; i < amount; i++)
                {
                    inventoryRepository.AddItem(new InventoryItem(itemDefinition));
                }

                break;
        }
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

    static bool MatchesTab(StoreItemDefinition storeItem, StoreTabType tab)
    {
        return tab switch
        {
            StoreTabType.Gold => storeItem.CostItemId == 201 || storeItem.CostItemId == 202,
            StoreTabType.Fate => storeItem.CostItemId == 221 || storeItem.CostItemId == 222,
            StoreTabType.Item203 => storeItem.CostItemId == 203,
            _ => false,
        };
    }
}
