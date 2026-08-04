using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

internal static class StoreConfigValidator
{
    internal static bool TryValidate(StoreConfigData config, ItemDatabase itemDatabase)
    {
        List<string> errors = new();
        if (config == null)
        {
            errors.Add("Config is null.");
        }
        else if (config.items == null || config.items.Count == 0)
        {
            errors.Add("Items is null or empty.");
        }

        if (itemDatabase == null)
        {
            errors.Add("ItemDatabase is unavailable.");
        }

        if (config?.items != null)
        {
            HashSet<int> storeItemIds = new();
            for (int index = 0; index < config.items.Count; index++)
            {
                StoreConfigItemData item = config.items[index];
                string itemPath = $"Item[{index}]";
                if (item == null)
                {
                    errors.Add($"{itemPath} is null.");
                    continue;
                }

                if (item.StoreItemId <= 0)
                {
                    errors.Add($"{itemPath} has an invalid storeItemId.");
                }
                else if (!storeItemIds.Add(item.StoreItemId))
                {
                    errors.Add($"Store item id is duplicated: {item.StoreItemId}.");
                }

                if (!item.category.HasValue ||
                    !Enum.IsDefined(typeof(StoreCategory), item.Category))
                {
                    errors.Add($"Store item '{item.StoreItemId}' has an invalid category.");
                }

                if (item.Order < 0)
                {
                    errors.Add($"Store item '{item.StoreItemId}' has a negative order.");
                }

                if (item.Count <= 0)
                {
                    errors.Add($"Store item '{item.StoreItemId}' count must be greater than zero.");
                }

                if (item.Price < 0)
                {
                    errors.Add($"Store item '{item.StoreItemId}' price cannot be negative.");
                }

                if (item.DiscountPercent < 0 || item.DiscountPercent > 100)
                {
                    errors.Add($"Store item '{item.StoreItemId}' discountPercent must be between 0 and 100.");
                }

                if (item.DailyPurchaseLimit <= 0)
                {
                    errors.Add($"Store item '{item.StoreItemId}' dailyPurchaseLimit must be greater than zero.");
                }

                if (itemDatabase == null)
                {
                    continue;
                }

                if (itemDatabase.GetItemByID(item.ItemId) == null)
                {
                    errors.Add($"Store item '{item.StoreItemId}' item is missing: {item.ItemId}.");
                }

                if (itemDatabase.GetItemByID(item.CostItemId) == null)
                {
                    errors.Add($"Store item '{item.StoreItemId}' cost item is missing: {item.CostItemId}.");
                }
            }
        }

        if (errors.Count == 0)
        {
            return true;
        }

        StringBuilder message = new("Store config validation failed:");
        foreach (string error in errors)
        {
            message.Append("\n- ");
            message.Append(error);
        }

        Debug.LogError(message.ToString());
        return false;
    }
}
