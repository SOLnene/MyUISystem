using System;
using UniRx;
using UnityEngine;

public class StoreItemViewModel : IDisposable
{
    readonly StoreItemDefinition storeItem;
    readonly StorePurchaseService purchaseService;

    public readonly ReactiveProperty<StorePurchasePreview> PurchasePreview = new();

    public int StoreItemId => storeItem.StoreItemId;
    public int CostItemId => storeItem.CostItemId;
    public StoreCategory Category => storeItem.Category;
    public ItemDefinition ItemDefinition { get; }
    public string Id { get; }
    public string Name { get; }
    public string IconPath { get; }
    public string CostIconPath { get; }
    public int CostValue => storeItem.Price;
    public int BeforeValue { get; }
    public int DiscountPercent => storeItem.DiscountPercent;
    public Color BackgroundColor { get; }
    public bool HasBeforeValue => BeforeValue > 0;
    public bool HasRemainCount => true;
    public bool HasDiscount => storeItem.HasDiscount;

    internal StoreItemViewModel(
        StoreItemDefinition storeItem,
        ItemDefinition itemDefinition,
        string costIconPath,
        Color backgroundColor,
        int beforeValue,
        StorePurchaseService purchaseService)
    {
        this.storeItem = storeItem;
        this.purchaseService = purchaseService;
        ItemDefinition = itemDefinition;
        Id = storeItem.ItemId.ToString();
        Name = storeItem.Count > 1 ? $"{itemDefinition.itemName}x{storeItem.Count}" : itemDefinition.itemName;
        IconPath = itemDefinition.iconPath;
        CostIconPath = costIconPath;
        BackgroundColor = backgroundColor;
        BeforeValue = beforeValue;

        RefreshPurchasePreview();
    }

    public void RefreshPurchasePreview()
    {
        PurchasePreview.Value = purchaseService.CreatePreview(storeItem);
    }

    public void Dispose()
    {
        PurchasePreview.Dispose();
    }
}
