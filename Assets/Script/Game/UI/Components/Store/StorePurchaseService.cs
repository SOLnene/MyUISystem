using System;
using UnityEngine;
using UniRx;

public readonly struct StorePurchasePreview
{
    public readonly int MaxPurchaseCount;
    public readonly int RemainingLimit;
    public readonly int AffordableCount;

    public StorePurchasePreview(int maxPurchaseCount, int remainingLimit, int affordableCount)
    {
        MaxPurchaseCount = Mathf.Max(0, maxPurchaseCount);
        RemainingLimit = Mathf.Max(0, remainingLimit);
        AffordableCount = Mathf.Max(0, affordableCount);
    }
}

class StorePurchaseService
{
    const int DailyPurchaseLimit = 10;

    static readonly StorePurchaseRepository purchaseRepository = new();
    public IObservable<int> Changed => purchaseRepository.Changed;

    public StorePurchasePreview CreatePreview(StoreItemDefinition storeItem)
    {
        int remainingLimit = purchaseRepository.GetRemainingCount(storeItem.StoreItemId, DailyPurchaseLimit);
        int affordableCount = storeItem.Price <= 0
            ? remainingLimit
            : GameEconomy.Instance.GetCurrency(storeItem.CostItemId) / storeItem.Price;
        int maxPurchaseCount = Mathf.Min(remainingLimit, affordableCount);

        return new StorePurchasePreview(maxPurchaseCount, remainingLimit, affordableCount);
    }

    public bool TryPurchase(StoreItemDefinition storeItem, ItemDefinition itemDefinition, int purchaseCount)
    {
        if (purchaseCount <= 0)
        {
            return false;
        }

        StorePurchasePreview preview = CreatePreview(storeItem);
        if (purchaseCount > preview.MaxPurchaseCount)
        {
            return false;
        }

        int totalPrice = storeItem.Price * purchaseCount;
        if (!GameEconomy.Instance.TrySpendCurrency(storeItem.CostItemId, totalPrice))
        {
            return false;
        }

        AddPurchasedItem(itemDefinition, storeItem.Count * purchaseCount);
        purchaseRepository.AddPurchasedCount(storeItem.StoreItemId, purchaseCount);
        return true;
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
}
