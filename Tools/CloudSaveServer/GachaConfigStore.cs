using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CloudSaveServer;

internal sealed class GachaConfigStore(string root)
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };
    static readonly Regex KeyPattern = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

    internal async Task<GachaConfigSnapshot> ReadCurrentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var schedule = await ReadAsync<GachaSchedule>("gacha-schedule.json", cancellationToken);
        if (schedule.Version <= 0 || schedule.Pools is null || schedule.Pools.Count > 64)
            throw new InvalidDataException("Invalid gacha schedule version or pool list.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var active = new List<GachaPoolConfig>();
        var commonPools = new Dictionary<string, CharacterEventCommon>(StringComparer.Ordinal);
        foreach (var slot in schedule.Pools)
        {
            if (slot is null || string.IsNullOrEmpty(slot.GachaKey) || !KeyPattern.IsMatch(slot.GachaKey) ||
                !keys.Add(slot.GachaKey) || slot.Enabled is null)
                throw new InvalidDataException("Invalid or duplicate gacha schedule key.");
            var start = ParseTime(slot.StartsAtUtc);
            var end = ParseTime(slot.EndsAtUtc);
            if (start.HasValue && end.HasValue && start >= end)
                throw new InvalidDataException("Gacha schedule end must follow start.");
            // 排期由服务端时间决定，结束时刻不再包含在开放区间内。
            if (!slot.Enabled.Value || (start.HasValue && now < start) || (end.HasValue && now >= end))
                continue;
            var source = await ReadAsync<GachaPoolSource>("gacha-" + slot.GachaKey + ".json", cancellationToken);
            GachaPoolConfig pool;
            if (source.CommonPool != null)
            {
                if (!KeyPattern.IsMatch(source.CommonPool))
                    throw new InvalidDataException("Invalid shared event configuration key.");
                if (!commonPools.TryGetValue(source.CommonPool, out var common))
                {
                    common = await ReadAsync<CharacterEventCommon>("gacha-common-" + source.CommonPool + ".json", cancellationToken);
                    // One request uses one common snapshot even if the file changes while individual pools are read.
                    commonPools.Add(source.CommonPool, common);
                }
                pool = CharacterEventConfigBuilder.Build(source, common);
            }
            else
            {
                if (source.FeaturedFiveStarKey != null)
                    throw new InvalidDataException("A featured event character requires a common configuration.");
                pool = new GachaPoolConfig(source.GachaKey, source.PityCount, source.Entries!);
            }
            ValidatePool(pool, slot.GachaKey);
            active.Add(pool);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new GachaConfigSnapshot(schedule.Version, now.ToUniversalTime().ToString("O"), active);
    }

    static DateTimeOffset? ParseTime(string? text)
    {
        if (text is null) return null;
        if (!DateTimeOffset.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "O"],
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) ||
            time.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Schedule times must be UTC ISO 8601.");
        return time;
    }

    static void ValidatePool(GachaPoolConfig pool, string expectedKey)
    {
        if (pool.GachaKey != expectedKey || pool.PityCount is < 1 or > 10000 ||
            pool.Entries is null || pool.Entries.Count is < 1 or > 1024)
            throw new InvalidDataException("Invalid gacha pool definition.");
        long total = 0;
        var entries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pool.Entries)
        {
            if (entry is null || string.IsNullOrEmpty(entry.EntryKey) || !KeyPattern.IsMatch(entry.EntryKey) ||
                entry.EntryType is not (0 or 1) || entry.Weight <= 0 || entry.Rarity is < 1 or > 5 ||
                !entries.Add(entry.EntryType + ":" + entry.EntryKey))
                throw new InvalidDataException("Invalid or duplicate gacha entry.");
            total += entry.Weight;
        }
        if (total > int.MaxValue)
            throw new InvalidDataException("Gacha entry weights exceed the supported range.");
    }

    async Task<T> ReadAsync<T>(string name, CancellationToken cancellationToken) where T : class
    {
        string path = Path.Combine(root, name);
        if (new FileInfo(path).Length > 1024 * 1024)
            throw new InvalidDataException("Gacha configuration exceeds the size limit.");
        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<T>(json, Options) ??
            throw new InvalidDataException("Gacha configuration is empty.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GachaSchedule([property: JsonRequired] long Version,
    [property: JsonRequired] List<GachaScheduleSlot> Pools);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GachaScheduleSlot([property: JsonRequired] string GachaKey,
    [property: JsonRequired] bool? Enabled, string? StartsAtUtc, string? EndsAtUtc);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GachaPoolConfig([property: JsonRequired] string GachaKey,
    [property: JsonRequired] int PityCount, [property: JsonRequired] List<GachaEntryConfig> Entries,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CharacterEventRules? EventRules = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GachaEntryConfig([property: JsonRequired] string EntryKey,
    [property: JsonRequired] int EntryType, [property: JsonRequired] int Weight,
    [property: JsonRequired] int Rarity);

internal sealed record GachaConfigSnapshot(long Version, string ServerTimeUtc, List<GachaPoolConfig> Pools);

