using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CloudSaveServer;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record StoreOffer(int StoreItemId, string Category, int Order, int ItemId, int Count,
    int CostItemId, [property: JsonRequired] int Price, int DiscountPercent, int DailyPurchaseLimit,
    bool Enabled = true);
internal sealed record StoreConfiguration(int SchemaVersion, long Version, int UtcOffsetMinutes,
    int ResetHour, List<StoreOffer> Items);
internal sealed record StoreListing(StoreOffer Offer, int RemainingLimit);
internal sealed record StoreSnapshot(string ConfigVersion, DateTimeOffset ServerTime,
    DateTimeOffset NextRefreshAt, string PeriodKey, List<StoreListing> Items);
internal sealed record StorePeriod(string Key, DateTimeOffset NextRefreshAt);
internal sealed record StoreDefinition(StoreConfiguration Configuration, string Token)
{
    internal StorePeriod Period(DateTimeOffset now)
    {
        var local = now.ToOffset(TimeSpan.FromMinutes(Configuration.UtcOffsetMinutes));
        var start = new DateTimeOffset(local.Year, local.Month, local.Day,
            Configuration.ResetHour, 0, 0, local.Offset);
        if (local < start) start = start.AddDays(-1);
        return new(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), start.AddDays(1).ToUniversalTime());
    }
}

internal sealed class StoreConfigStore(string root)
{
    internal async Task<StoreDefinition> ReadAsync(CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(Path.Combine(root, "store-config.json"), cancellationToken);
        var config = JsonSerializer.Deserialize<StoreConfiguration>(json, GachaJson.Options)
            ?? throw new InvalidDataException("Store configuration is missing.");
        if (config.SchemaVersion != 1 || config.Version <= 0 || config.UtcOffsetMinutes is < -720 or > 840 ||
            config.ResetHour is < 0 or > 23 || config.Items == null || config.Items.Count > 1000)
            throw new InvalidDataException("Invalid store configuration.");
        var catalog = await ReadCatalogAsync(cancellationToken);
        var ids = new HashSet<int>();
        foreach (var offer in config.Items)
        {
            if (offer == null || offer.StoreItemId <= 0 || !ids.Add(offer.StoreItemId) ||
                offer.Category is not ("Primogem" or "StarglitterStardust" or "Mora" or "GenesisCrystal") ||
                offer.Order < 0 || offer.Count is < 1 or > 1000000 || offer.Price < 0 ||
                offer.DiscountPercent is < 0 or > 100 || offer.DailyPurchaseLimit is < 1 or > 1000000 ||
                !catalog.TryGetValue(offer.ItemId, out var item) ||
                item.Category is not ("Currency" or "Material" or "ExpBook" or "Consumable" or "Equip") ||
                !catalog.TryGetValue(offer.CostItemId, out var cost) || cost.Category != "Currency")
                throw new InvalidDataException("Invalid store offer.");
        }
        // Fingerprint the content as well as the operator's version: forgotten version bumps cannot change a confirmed price.
        string token = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(config, GachaJson.Options)));
        return new(config, token);
    }

    internal async Task<Dictionary<int, StoreItemDefinition>> ReadCatalogAsync(CancellationToken cancellationToken)
    {
        var items = JsonSerializer.Deserialize<List<StoreItemDefinition>>(
            await File.ReadAllTextAsync(Path.Combine(root, "store-items.json"), cancellationToken), GachaJson.Options)
            ?? throw new InvalidDataException("Store item catalog is missing.");
        if (items.Any(i => i == null || i.Id <= 0) || items.Select(i => i.Id).Distinct().Count() != items.Count)
            throw new InvalidDataException("Invalid store item catalog.");
        return items.ToDictionary(i => i.Id);
    }
}
internal sealed record StoreItemDefinition(int Id, string Category, int Stars);

internal static class StoreSave
{
    internal static JsonObject Section(JsonObject root, string key)
    {
        if (root[key] == null) root[key] = new JsonObject();
        return root[key] as JsonObject ?? throw new StoreFailure("invalid_store_save", 400);
    }
    internal static JsonArray Array(JsonObject root, string key)
    {
        if (root[key] == null) root[key] = new JsonArray();
        return root[key] as JsonArray ?? throw new StoreFailure("invalid_store_save", 400);
    }
    internal static Dictionary<int, JsonObject> Balances(JsonObject save)
    {
        var result = new Dictionary<int, JsonObject>();
        foreach (var node in Array(Section(save, "currencies"), "items"))
        {
            if (node is not JsonObject item || item["itemId"] is not JsonValue idValue ||
                !idValue.TryGetValue<int>(out int id) || id <= 0 ||
                item["amount"] is not JsonValue amountValue || !amountValue.TryGetValue<int>(out int amount) ||
                amount < 0 || !result.TryAdd(id, item)) throw new StoreFailure("invalid_store_save", 400);
        }
        return result;
    }

    internal static void Grant(JsonObject save, StoreItemDefinition definition, int amount)
    {
        if (definition.Category == "Currency")
        {
            var balances = Balances(save);
            if (balances.TryGetValue(definition.Id, out var currency))
                currency["amount"] = checked(currency["amount"]!.GetValue<int>() + amount);
            else Array(Section(save, "currencies"), "items").Add(new JsonObject
                { ["itemId"] = definition.Id, ["amount"] = amount });
            return;
        }
        var inventory = Section(save, "inventory");
        if (definition.Category == "Equip")
        {
            var equips = Array(inventory, "equips");
            var existingIds = equips.Select(e => e?["instanceId"]?.GetValue<long>() ?? 0).ToArray();
            if (existingIds.Any(id => id <= 0) || existingIds.Distinct().Count() != existingIds.Length)
                throw new StoreFailure("invalid_store_save", 400);
            long next = checked(existingIds.DefaultIfEmpty(0).Max() + 1);
            if (amount > 1000) throw new StoreFailure("store_quantity_too_large", 400);
            for (int i = 0; i < amount; i++)
            {
                long id = next; next = checked(next + 1);
                equips.Add(new JsonObject { ["instanceId"] = id, ["itemId"] = definition.Id,
                    ["level"] = 1, ["exp"] = 0, ["rank"] = 0, ["refinementLevel"] = 1 });
                if (definition.Stars >= 4) Array(inventory, "unseenEquipInstanceIds").Add(id);
            }
            return;
        }
        var stacks = Array(inventory, "stacks");
        var matches = stacks.OfType<JsonObject>().Where(i => i["itemId"]?.GetValue<int>() == definition.Id).ToArray();
        if (matches.Length > 1) throw new StoreFailure("invalid_store_save", 400);
        var stack = matches.SingleOrDefault();
        if (stack == null) stacks.Add(new JsonObject { ["itemId"] = definition.Id, ["count"] = amount });
        else
        {
            int prior = stack["count"]?.GetValue<int>() ?? -1;
            if (prior < 0) throw new StoreFailure("invalid_store_save", 400);
            stack["count"] = checked(prior + amount);
        }
        if (definition.Category is "Material" or "ExpBook")
        {
            var discovered = Array(inventory, "discoveredMaterialIds");
            if (!discovered.Any(i => i?.GetValue<int>() == definition.Id))
            {
                discovered.Add(definition.Id);
                Array(inventory, "unseenMaterialIds").Add(definition.Id);
            }
        }
    }
}
