using System.Text.Json;
using System.Text.Json.Nodes;

namespace CloudSaveServer;

internal sealed class GachaCatalog(string path)
{
    readonly Lazy<CatalogData> data = new(() => JsonSerializer.Deserialize<CatalogData>(
        File.ReadAllText(path), GachaJson.Options) ?? throw new InvalidDataException("Missing gacha catalog."));

    internal void Validate(GachaPoolConfig pool)
    {
        if (data.Value.SchemaVersion != 1) throw new InvalidDataException("Unsupported gacha catalog.");
        foreach (var entry in pool.Entries)
        {
            if (entry.EntryType == 0) _ = Character(entry.EntryKey);
            else _ = Weapon(entry.EntryKey);
        }
    }
    CatalogCharacter Character(string key) => data.Value.Characters.SingleOrDefault(c => c.Key == key)
        ?? throw new InvalidDataException("Unknown gacha character: " + key);
    CatalogWeapon Weapon(string key) => data.Value.Weapons.SingleOrDefault(w => w.Key == key)
        ?? throw new InvalidDataException("Unknown gacha weapon: " + key);

    internal List<GachaAward> Grant(JsonObject save, IReadOnlyList<GachaEntryConfig> entries, ref long nextId)
    {
        var inventory = Section(save, "inventory");
        var equips = Array(inventory, "equips");
        var characters = Array(Section(save, "characters"), "characters");
        var ids = new HashSet<long>();
        foreach (var node in equips)
        {
            var equip = node as JsonObject ?? throw new GachaFailure("invalid_gacha_save", 400);
            long id = equip["instanceId"]?.GetValue<long>() ?? 0;
            if (id <= 0 || !ids.Add(id)) throw new GachaFailure("invalid_gacha_save", 400);
            nextId = Math.Max(nextId, checked(id + 1));
        }
        if (characters.OfType<JsonObject>().Select(c => c["characterKey"]?.GetValue<string>())
            .Distinct(StringComparer.Ordinal).Count() != characters.Count)
            throw new GachaFailure("invalid_gacha_save", 400);
        long cursor = Math.Max(nextId, 1);
        long AddWeapon(string key)
        {
            var weapon = Weapon(key);
            long id = cursor;
            cursor = checked(cursor + 1);
            equips.Add(new JsonObject
            {
                ["instanceId"] = id, ["itemId"] = weapon.ItemId, ["level"] = 1, ["exp"] = 0,
                ["rank"] = 0, ["refinementLevel"] = 1
            });
            if (weapon.Rarity >= 4)
                Array(inventory, "unseenEquipInstanceIds").Add(id);
            return id;
        }
        var results = new List<GachaAward>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.EntryType == 1)
            {
                results.Add(new GachaAward(entry.EntryKey, 1, entry.Rarity, AddWeapon(entry.EntryKey), false));
                continue;
            }
            var definition = Character(entry.EntryKey);
            var existing = characters.OfType<JsonObject>().SingleOrDefault(c =>
                c["characterKey"]?.GetValue<string>() == entry.EntryKey);
            bool isNew = existing == null;
            if (isNew)
            {
                var starter = Weapon(definition.StarterWeaponKey);
                if (starter.WeaponType != definition.WeaponType || starter.Rarity != 3)
                    throw new InvalidDataException("Invalid gacha starter weapon.");
                characters.Add(new JsonObject
                {
                    ["characterKey"] = entry.EntryKey, ["level"] = 1, ["exp"] = 0, ["rank"] = 0,
                    ["talentLevel"] = 0, ["talentTokenCount"] = 0,
                    ["equippedWeaponInstanceId"] = AddWeapon(definition.StarterWeaponKey)
                });
            }
            else
            {
                int tokens = existing!["talentTokenCount"]?.GetValue<int>() ?? 0;
                if (tokens < 0) throw new GachaFailure("invalid_gacha_save", 400);
                existing["talentTokenCount"] = checked(tokens + 1);
            }
            results.Add(new GachaAward(entry.EntryKey, 0, entry.Rarity, 0, isNew));
        }
        nextId = cursor;
        return results;
    }

    internal static JsonObject Section(JsonObject parent, string name)
    {
        if (parent[name] == null) parent[name] = new JsonObject();
        return parent[name] as JsonObject ?? throw new GachaFailure("invalid_gacha_save", 400);
    }
    internal static JsonArray Array(JsonObject parent, string name)
    {
        if (parent[name] == null) parent[name] = new JsonArray();
        return parent[name] as JsonArray ?? throw new GachaFailure("invalid_gacha_save", 400);
    }

    sealed record CatalogData(int SchemaVersion, List<CatalogWeapon> Weapons, List<CatalogCharacter> Characters);
    sealed record CatalogWeapon(string Key, int ItemId, string WeaponType, int Rarity);
    sealed record CatalogCharacter(string Key, string WeaponType, string StarterWeaponKey);
}
