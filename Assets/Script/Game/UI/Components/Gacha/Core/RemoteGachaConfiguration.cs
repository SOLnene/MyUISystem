using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Domain.Character;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
internal sealed class GachaConfigSnapshot
{
    public long version = 0;
    public string serverTimeUtc = null;
    public RemoteGachaPoolData[] pools = null;
}

[Serializable]
internal sealed class RemoteGachaPoolData
{
    public string gachaKey = null;
    public int pityCount = 0;
    public RemoteGachaEntryData[] entries = null;
    public GachaEventRules eventRules = null;
}

[Serializable]
internal sealed class RemoteGachaEntryData
{
    public string entryKey = null;
    public int entryType = -1;
    public int weight = 0;
    public int rarity = 0;
}

internal sealed class RemoteGachaConfiguration : IGachaPoolProvider, IDisposable
{
    readonly Dictionary<string, GachaDefinition> pools = new Dictionary<string, GachaDefinition>(StringComparer.Ordinal);
    readonly List<GachaPoolUIConfig> uiConfigs = new List<GachaPoolUIConfig>();
    internal IReadOnlyList<GachaPoolUIConfig> UiConfigs => uiConfigs;
    internal long Version { get; private set; }

    internal static GachaConfigSnapshot ParseSnapshot(string json)
    {
        // JsonUtility materializes absent inline objects; preserve null so legacy pools keep their original rules.
        try
        {
            return JsonConvert.DeserializeObject<GachaConfigSnapshot>(json) ??
                throw new InvalidDataException("服务器返回的卡池配置无效。");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("服务器返回的卡池配置无效。", exception);
        }
    }

    internal static RemoteGachaConfiguration Create(GachaConfigSnapshot snapshot,
        GachaPoolUIConfigDatabase visuals, CharacterDatabase characters, ItemDatabase items)
    {
        if (snapshot == null || snapshot.version <= 0 || snapshot.pools == null || snapshot.pools.Length > 64 ||
            !DateTimeOffset.TryParse(snapshot.serverTimeUtc, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var serverTime) || serverTime.Offset != TimeSpan.Zero)
            throw new InvalidDataException("服务器返回的卡池配置无效。");
        var result = new RemoteGachaConfiguration { Version = snapshot.version };
        var policies = new Dictionary<string, RemoteGachaPoolData>(StringComparer.Ordinal);
        try
        {
            foreach (var data in snapshot.pools)
            {
                if (data == null || string.IsNullOrEmpty(data.gachaKey) || result.pools.ContainsKey(data.gachaKey) ||
                    data.pityCount < 1 || data.pityCount > 10000 || data.entries == null ||
                    data.entries.Length < 1 || data.entries.Length > 1024)
                    throw new InvalidDataException("服务器返回的卡池定义无效。");
                var visual = visuals.GetConfig(data.gachaKey);
                if (visual == null)
                    throw new InvalidDataException($"当前客户端没有卡池 {data.gachaKey} 的显示资源。");
                long total = 0;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                var entries = new List<GachaEntry>();
                foreach (var entry in data.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.entryKey) ||
                        (entry.entryType != 0 && entry.entryType != 1) || entry.weight <= 0 ||
                        entry.rarity < 1 || entry.rarity > 5 ||
                        !keys.Add(entry.entryType + ":" + entry.entryKey))
                        throw new InvalidDataException("服务器返回的卡池物品或权重无效。");
                    bool known = entry.entryType == (int)GachaEntryType.Character
                        ? characters.Get(entry.entryKey) != null
                        : items.GetItemByKey(entry.entryKey) is EquipDefinition;
                    if (!known)
                        throw new InvalidDataException($"当前客户端没有卡池物品 {entry.entryKey} 的定义。");
                    total += entry.weight;
                    entries.Add(new GachaEntry
                    {
                        entryKey = entry.entryKey, entryType = (GachaEntryType)entry.entryType,
                        weight = entry.weight, rarity = entry.rarity
                    });
                }
                if (total > int.MaxValue)
                    throw new InvalidDataException("服务器返回的卡池权重超出范围。");
                if (data.eventRules != null)
                {
                    data.eventRules.Validate(data.pityCount, entries);
                    if (policies.TryGetValue(data.eventRules.pityGroup, out var previous) &&
                        !data.eventRules.SharesPolicyWith(previous.eventRules, data.pityCount, previous.pityCount))
                        throw new InvalidDataException("共享保底的活动卡池规则不一致。");
                    policies[data.eventRules.pityGroup] = data;
                }
                // 使用临时定义，服务器配置不能改写项目中的 ScriptableObject 资源。
                var pool = ScriptableObject.CreateInstance<GachaDefinition>();
                pool.hideFlags = HideFlags.HideAndDontSave;
                pool.gachaKey = data.gachaKey;
                pool.pityCount = data.pityCount;
                pool.entries = entries;
                pool.EventRules = data.eventRules;
                result.pools.Add(data.gachaKey, pool);
                result.uiConfigs.Add(visual);
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public GachaDefinition GetPool(string gachaKey) =>
        !string.IsNullOrEmpty(gachaKey) && pools.TryGetValue(gachaKey, out var pool) ? pool : null;

    public void Dispose()
    {
        foreach (var pool in pools.Values)
            UnityEngine.Object.Destroy(pool);
        pools.Clear();
        uiConfigs.Clear();
    }
}

