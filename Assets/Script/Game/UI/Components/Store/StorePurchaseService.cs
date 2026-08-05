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
    readonly StorePurchaseRepository purchaseRepository;
    public IObservable<int> Changed => purchaseRepository.Changed;

    public StorePurchaseService(StorePurchaseRepository purchaseRepository)
    {
        this.purchaseRepository = purchaseRepository;
    }

    public StorePurchasePreview CreatePreview(StoreConfigItemData storeItem)
    {
        int remainingLimit = purchaseRepository.GetRemainingCount(
            storeItem.StoreItemId,
            storeItem.DailyPurchaseLimit);
        int affordableCount = storeItem.Price <= 0
            ? remainingLimit
            : GameEconomy.Instance.GetCurrency(storeItem.CostItemId) / storeItem.Price;
        int maxPurchaseCount = Mathf.Min(remainingLimit, affordableCount);

        return new StorePurchasePreview(maxPurchaseCount, remainingLimit, affordableCount);
    }

    public bool TryPurchase(StoreConfigItemData storeItem, ItemDefinition itemDefinition, int purchaseCount)
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

        if (!RewardGrantService.TryGrant(
                new[]
                {
                    new RewardItemData(
                        itemDefinition.id,
                        storeItem.Count * purchaseCount)
                }))
        {
            GameEconomy.Instance.AddCurrency(storeItem.CostItemId, totalPrice);
            return false;
        }

        purchaseRepository.AddPurchasedCount(storeItem.StoreItemId, purchaseCount);
        AchievementProgressService.Instance.AddProgress(
            AchievementProgressKeys.StorePurchase,
            purchaseCount);
        GameSaveCoordinator.Instance.MarkDirty();
        return true;
    }

    public StorePurchaseSaveData ExportSaveData()
    {
        return purchaseRepository.ExportSaveData();
    }

    public void ImportSaveData(StorePurchaseSaveData saveData)
    {
        purchaseRepository.ImportSaveData(saveData);
    }
}
