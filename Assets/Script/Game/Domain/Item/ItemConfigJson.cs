using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

public static class ItemConfigJson
{
    public const string FileName = "ItemDatabase.json";

    public static ItemDatabase Parse(string json)
    {
        try
        {
            var root = JObject.Parse(json, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (root["schemaVersion"]?.Type != JTokenType.Integer ||
                root["schemaVersion"].ToString() != "1" || root["items"] is not JArray rows || rows.Count == 0)
                throw new InvalidDataException("Item config requires schemaVersion 1 and a nonempty items array.");

            var serializer = CreateSerializer();
            var items = new List<ItemDefinition>();
            var ids = new HashSet<int>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in rows)
            {
                if (token is not JObject row || row["id"]?.Type != JTokenType.Integer ||
                    row["stars"]?.Type != JTokenType.Integer || row["category"]?.Type != JTokenType.String ||
                    row["itemRarity"]?.Type != JTokenType.String)
                    throw new InvalidDataException("Item config contains invalid or missing identity fields.");
                foreach (string field in new[] { "key", "itemName", "desc", "iconPath" })
                    if (row[field]?.Type != JTokenType.String)
                        throw new InvalidDataException($"Item config requires a string field: {field}.");
                if (!Enum.TryParse(row.Value<string>("category"), out ItemCategory category) ||
                    !Enum.IsDefined(typeof(ItemCategory), category))
                    throw new InvalidDataException("Item config contains an invalid category.");

                // Category is the discriminator: weapon consumers rely on the derived runtime type.
                ItemDefinition item = category == ItemCategory.Equip
                    ? row.ToObject<EquipDefinition>(serializer)
                    : row.ToObject<ItemDefinition>(serializer);
                ValidateDefinition(item);
                if (!ids.Add(item.id) || !keys.Add(item.key))
                    throw new InvalidDataException($"Item config contains a duplicate ID or key: {item.id}/{item.key}.");
                items.Add(item);
            }
            return new ItemDatabase { allItems = items };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Item config JSON is invalid.", exception);
        }
    }

    public static string Serialize(IReadOnlyList<ItemDefinition> items)
    {
        if (items == null || items.Count == 0)
            throw new InvalidDataException("Item config cannot be empty.");
        var rows = new JArray();
        var serializer = CreateSerializer();
        foreach (var item in items)
        {
            ValidateDefinition(item);
            var row = JObject.FromObject(item, serializer);
            row.Remove(nameof(EquipDefinition.MaxRank));
            rows.Add(row);
        }
        string json = new JObject { ["schemaVersion"] = 1, ["items"] = rows }.ToString(Formatting.Indented);
        // Validate the complete merge before an editor tool replaces the existing table.
        Parse(json);
        return json;
    }

    static JsonSerializer CreateSerializer() => JsonSerializer.Create(new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        MissingMemberHandling = MissingMemberHandling.Error,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Converters = { new StringEnumConverter { AllowIntegerValues = false } }
    });

    static void ValidateDefinition(ItemDefinition item)
    {
        if (item == null || item.id <= 0 || string.IsNullOrWhiteSpace(item.key) ||
            string.IsNullOrWhiteSpace(item.itemName) || string.IsNullOrWhiteSpace(item.iconPath) ||
            !Enum.IsDefined(typeof(ItemCategory), item.category) ||
            !Enum.IsDefined(typeof(ItemRarity), item.itemRarity) || item.itemRarity == ItemRarity.Max ||
            item.stars < 1 || item.stars > 5 || (item is EquipDefinition) != (item.category == ItemCategory.Equip))
            throw new InvalidDataException("Item config contains an invalid definition.");
        if (item is not EquipDefinition equip)
            return;
        if (equip.baseAttack < 0 || !IsNonnegativeFinite(equip.baseCritRate) || equip.baseCritRate > 1f ||
            !IsNonnegativeFinite(equip.baseCritDamage) || equip.rankInfos == null || equip.rankInfos.Count == 0)
            throw new InvalidDataException($"Weapon config contains invalid stats: {equip.key}.");
        int previousMaxLevel = 0;
        // Legacy rank labels may repeat; rank lookup uses the list position.
        for (int index = 0; index < equip.rankInfos.Count; index++)
        {
            var rank = equip.rankInfos[index];
            if (rank == null || rank.rank < 0 || rank.maxLevel <= previousMaxLevel || rank.goldCost < 0 ||
                !IsNonnegativeFinite(rank.attackAddFlat) || !IsNonnegativeFinite(rank.attackAddPercent) ||
                rank.requirements == null)
                throw new InvalidDataException($"Weapon config contains an invalid rank: {equip.key}/{index}.");
            foreach (var cost in rank.requirements)
                if (cost == null || string.IsNullOrWhiteSpace(cost.materialKey) || cost.count <= 0)
                    throw new InvalidDataException($"Weapon config contains an invalid material cost: {equip.key}/{index}.");
            previousMaxLevel = rank.maxLevel;
        }
    }

    static bool IsNonnegativeFinite(float value) => value >= 0f && !float.IsInfinity(value);
}
