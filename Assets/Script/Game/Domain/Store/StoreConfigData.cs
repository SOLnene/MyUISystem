using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[Serializable]
public sealed class StoreConfigData
{
    public List<StoreConfigItemData> items = new();
}

[Serializable]
public sealed class StoreConfigItemData
{
    public int storeItemId;
    [JsonConverter(typeof(StringEnumConverter))]
    public StoreCategory? category;
    public int order;
    public int itemId;
    public int count;
    public int costItemId;
    public int price;
    public int discountPercent;
    public int dailyPurchaseLimit;

    public int StoreItemId => storeItemId;
    public StoreCategory Category => category.Value;
    public int Order => order;
    public int ItemId => itemId;
    public int Count => count;
    public int CostItemId => costItemId;
    public int Price => price;
    public int DiscountPercent => discountPercent;
    public int DailyPurchaseLimit => dailyPurchaseLimit;
    public bool HasDiscount => discountPercent > 0;
}
