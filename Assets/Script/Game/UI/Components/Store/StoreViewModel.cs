using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class StoreViewModel : IDisposable
{
    const int PrimogemItemId = 201;
    const int MoraItemId = 202;
    const int GenesisCrystalItemId = 203;
    const int StarglitterItemId = 221;
    const int StardustItemId = 222;

    readonly StoreConfigDatabase storeDatabase;
    readonly ItemDatabase itemDatabase;
    readonly StorePurchaseService purchaseService;
    readonly CompositeDisposable disposables = new();
    readonly List<StoreItemViewModel> allItemViewModels = new();
    readonly Dictionary<int, StoreItemViewModel> itemViewModelsById = new();
    readonly Dictionary<StoreCategory, List<StoreItemViewModel>> itemViewModelsByCategory = new();
    readonly Dictionary<int, List<StoreItemViewModel>> itemViewModelsByCostItemId = new();

    public readonly ReactiveProperty<StoreCategory> CurrentTab = new(StoreCategory.Primogem);
    public readonly ReactiveProperty<IReadOnlyList<StoreItemViewModel>> Items = new(new List<StoreItemViewModel>());

    public StoreViewModel(StoreConfigDatabase storeDatabase, ItemDatabase itemDatabase)
    {
        this.storeDatabase = storeDatabase;
        this.itemDatabase = itemDatabase;
        purchaseService = GameContext.Instance.StorePurchaseService;

        InitializeItemViewModels();
        ObserveCostCurrencies();

        CurrentTab
            .Subscribe(_ => RefreshVisibleItems())
            .AddTo(disposables);

        purchaseService.Changed
            .Subscribe(RefreshStoreItemPreview)
            .AddTo(disposables);
    }

    void InitializeItemViewModels()
    {
        if (storeDatabase == null || itemDatabase == null)
        {
            Debug.LogWarning("StoreViewModel cannot load items because database is not ready.");
            return;
        }

        foreach (StoreConfigItemData storeItem in storeDatabase.Items)
        {
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

            var itemViewModel = new StoreItemViewModel(
                storeItem,
                itemDefinition,
                costDefinition?.iconPath,
                rarityColor,
                beforeValue,
                purchaseService);
            allItemViewModels.Add(itemViewModel);
            itemViewModelsById[storeItem.StoreItemId] = itemViewModel;
            if (!itemViewModelsByCategory.TryGetValue(
                    itemViewModel.Category,
                    out List<StoreItemViewModel> categoryItemViewModels))
            {
                categoryItemViewModels = new List<StoreItemViewModel>();
                itemViewModelsByCategory.Add(itemViewModel.Category, categoryItemViewModels);
            }

            categoryItemViewModels.Add(itemViewModel);
            if (!itemViewModelsByCostItemId.TryGetValue(
                    itemViewModel.CostItemId,
                    out List<StoreItemViewModel> itemViewModels))
            {
                itemViewModels = new List<StoreItemViewModel>();
                itemViewModelsByCostItemId.Add(itemViewModel.CostItemId, itemViewModels);
            }

            itemViewModels.Add(itemViewModel);
        }

        foreach (List<StoreItemViewModel> categoryItemViewModels in itemViewModelsByCategory.Values)
        {
            categoryItemViewModels.Sort(CompareItems);
        }
    }

    void ObserveCostCurrencies()
    {
        foreach (int costItemId in itemViewModelsByCostItemId.Keys)
        {
            GameEconomy.Instance.ObserveCurrency(costItemId)
                .Skip(1)
                .Subscribe(_ => RefreshCurrencyRelatedPreviews(costItemId))
                .AddTo(disposables);
        }
    }

    public void SetTab(StoreCategory category)
    {
        CurrentTab.Value = category;
    }

    public void Dispose()
    {
        disposables.Dispose();
        foreach (StoreItemViewModel itemViewModel in allItemViewModels)
        {
            itemViewModel.Dispose();
        }
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

        if (!storeDatabase.TryGetItem(storeItemId, out StoreConfigItemData storeItem))
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

        if (!storeDatabase.TryGetItem(storeItemId, out StoreConfigItemData storeItem))
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

    static int CalculateBeforeValue(StoreConfigItemData storeItem)
    {
        if (!storeItem.HasDiscount || storeItem.DiscountPercent >= 100)
        {
            return 0;
        }

        float discountRate = (100 - storeItem.DiscountPercent) / 100f;
        return Mathf.CeilToInt(storeItem.Price / discountRate);
    }

    static bool MatchesCategory(StoreConfigItemData storeItem, StoreCategory category)
    {
        return storeItem.Category == category;
    }

    void RefreshVisibleItems()
    {
        if (itemViewModelsByCategory.TryGetValue(
                CurrentTab.Value,
                out List<StoreItemViewModel> itemViewModels))
        {
            Items.Value = itemViewModels;
            return;
        }

        Items.Value = Array.Empty<StoreItemViewModel>();
    }

    static int CompareItems(StoreItemViewModel left, StoreItemViewModel right)
    {
        int orderComparison = left.Order.CompareTo(right.Order);
        if (orderComparison != 0)
        {
            return orderComparison;
        }

        int rarityComparison = right.Rarity.CompareTo(left.Rarity);
        return rarityComparison != 0
            ? rarityComparison
            : left.StoreItemId.CompareTo(right.StoreItemId);
    }

    void RefreshStoreItemPreview(int storeItemId)
    {
        if (itemViewModelsById.TryGetValue(storeItemId, out StoreItemViewModel itemViewModel))
        {
            itemViewModel.RefreshPurchasePreview();
        }
    }

    void RefreshCurrencyRelatedPreviews(int costItemId)
    {
        if (!itemViewModelsByCostItemId.TryGetValue(
                costItemId,
                out List<StoreItemViewModel> itemViewModels))
        {
            return;
        }

        foreach (StoreItemViewModel itemViewModel in itemViewModels)
        {
            itemViewModel.RefreshPurchasePreview();
        }
    }
}
