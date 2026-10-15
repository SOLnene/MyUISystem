using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CloudSaveServer;

internal static class CharacterEventConfigBuilder
{
    static readonly Regex KeyPattern = new("\\A[A-Za-z0-9_-]{1,64}\\z", RegexOptions.CultureInvariant);

    internal static GachaPoolConfig Build(GachaPoolSource source, CharacterEventCommon common)
    {
        if (source.Entries != null || source.PityCount != 0 || !ValidKey(source.FeaturedFiveStarKey) || common.Rules == null)
            throw new InvalidDataException("An event pool must identify one featured character and one shared configuration.");
        var rules = common.Rules;
        if (!ValidKey(rules.PityGroup) || rules.FiveStarProbability <= 0 || rules.FourStarProbability <= 0 ||
            rules.FiveStarProbability + rules.FourStarProbability >= 1 || rules.FiveStarPityCount is < 1 or > 10000 ||
            rules.FourStarPityCount < 1 || rules.FourStarPityCount > rules.FiveStarPityCount ||
            rules.FeaturedFiveStarProbability <= 0 || rules.FeaturedFiveStarProbability > 1)
            throw new InvalidDataException("Invalid character event probability or pity rules.");
        var entries = new List<GachaEntryConfig>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        void Add(IEnumerable<string>? values, int type, int rarity)
        {
            if (values == null) throw new InvalidDataException("Shared event item lists are required.");
            foreach (string key in values)
            {
                if (!ValidKey(key) || !keys.Add(type + ":" + key))
                    throw new InvalidDataException("Invalid or repeated shared event item.");
                entries.Add(new GachaEntryConfig(key, type, 1, rarity));
            }
        }
        Add(common.ThreeStarWeapons, 1, 3);
        Add(common.FourStarWeapons, 1, 4);
        Add(common.StandardFiveStarCharacters, 0, 5);
        if (entries.Count(e => e.Rarity == 3) == 0 || entries.Count(e => e.Rarity == 4) == 0 || entries.Count > 1023)
            throw new InvalidDataException("Event pools need both ordinary weapon tiers within the item limit.");
        if (rules.FeaturedFiveStarProbability < 1 && !entries.Any(e => e.Rarity == 5 && e.EntryKey != source.FeaturedFiveStarKey))
            throw new InvalidDataException("A 50/50 event needs a non-featured standard character.");
        if (keys.Add("0:" + source.FeaturedFiveStarKey))
            entries.Add(new GachaEntryConfig(source.FeaturedFiveStarKey!, 0, 1, 5));
        return new GachaPoolConfig(source.GachaKey, rules.FiveStarPityCount, entries,
            new CharacterEventRules(rules.PityGroup, rules.FiveStarProbability, rules.FourStarProbability,
                rules.FourStarPityCount, source.FeaturedFiveStarKey!, rules.FeaturedFiveStarProbability));
    }

    static bool ValidKey(string? value) => value != null && KeyPattern.IsMatch(value);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GachaPoolSource([property: JsonRequired] string GachaKey, int PityCount = 0,
    List<GachaEntryConfig>? Entries = null, string? CommonPool = null, string? FeaturedFiveStarKey = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CharacterEventCommon([property: JsonRequired] CharacterEventCommonRules Rules,
    [property: JsonRequired] List<string> ThreeStarWeapons, [property: JsonRequired] List<string> FourStarWeapons,
    [property: JsonRequired] List<string> StandardFiveStarCharacters);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CharacterEventCommonRules([property: JsonRequired] string PityGroup,
    [property: JsonRequired] decimal FiveStarProbability, [property: JsonRequired] decimal FourStarProbability,
    [property: JsonRequired] int FiveStarPityCount, [property: JsonRequired] int FourStarPityCount,
    [property: JsonRequired] decimal FeaturedFiveStarProbability);

internal sealed record CharacterEventRules(string PityGroup, decimal FiveStarProbability, decimal FourStarProbability,
    int FourStarPityCount, string FeaturedFiveStarKey, decimal FeaturedFiveStarProbability);
