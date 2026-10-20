using System.Text.Json;
using CloudSaveServer;

internal static partial class Program
{
    static async Task ValidateCharacterEvents(string root)
    {
        Directory.CreateDirectory(root);
        string sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string[] roles = ["Hutao", "Eula", "Furina", "Keqing", "Citlali", "Columbina", "Odette"];
        foreach (string name in roles.Select(r => "gacha-CharPool_" + r + ".json").Append("gacha-common-character-event.json"))
            File.Copy(Path.Combine(sourceRoot, name), Path.Combine(root, name));
        var slots = roles.Select(r => new GachaScheduleSlot("CharPool_" + r, true, null, null)).ToList();
        await File.WriteAllTextAsync(Path.Combine(root, "gacha-schedule.json"),
            JsonSerializer.Serialize(new GachaSchedule(2, slots), GachaJson));
        var store = new GachaConfigStore(root);
        var snapshot = await store.ReadCurrentAsync(DateTimeOffset.UtcNow, default);
        Check(snapshot.Pools.Count == roles.Length, "All configured character event pools must load with one policy.");
        foreach (var pool in snapshot.Pools)
        {
            var rules = pool.EventRules!;
            Check(pool.PityCount == 90 && rules.FourStarPityCount == 10 && rules.PityGroup == "CharacterEvent",
                "Event pools must share the intended guarantee policy.");
            Check(rules.FiveStarProbability == 0.006m && rules.FourStarProbability == 0.051m &&
                rules.FeaturedFiveStarProbability == 0.5m, "Event rarity and featured probabilities are explicit.");
            Check(pool.Entries.Single(e => e.EntryKey == rules.FeaturedFiveStarKey).Rarity == 5 &&
                pool.Entries.All(e => e.Weight == 1), "One featured five-star and uniform intra-tier weights are required.");
            Check(pool.Entries.Any(e => e.Rarity == 3) && pool.Entries.Any(e => e.Rarity == 4) &&
                pool.Entries.Where(e => e.Rarity < 5).All(e => e.EntryType == 1), "Lower tiers contain weapons only.");
            Check(pool.Entries.Any(e => e.Rarity == 5 && e.EntryKey != rules.FeaturedFiveStarKey) &&
                !pool.Entries.Any(e => e.EntryKey == "lumine"), "50/50 needs a standard alternative; Lumine is excluded.");
        }
        Check(snapshot.Pools.All(pool => pool.Entries.All(e => e.EntryKey != "Claymore_Aniki" &&
            e.EntryKey != "chongyun" && e.EntryKey != "yunjin" && e.EntryKey != "ningguang")),
            "Legacy training weapons and unavailable four-star previews must not enter new event rewards.");
        await File.WriteAllTextAsync(Path.Combine(root, "client-snapshot.json"), JsonSerializer.Serialize(snapshot, GachaJson));
        string commonPath = Path.Combine(root, "gacha-common-character-event.json");
        string originalCommon = await File.ReadAllTextAsync(commonPath);
        var common = JsonSerializer.Deserialize<CharacterEventCommon>(originalCommon, GachaJson)!;
        await File.WriteAllTextAsync(commonPath, JsonSerializer.Serialize(common with
        {
            Rules = common.Rules with { FourStarProbability = 0.1m },
            FourStarWeapons = [common.FourStarWeapons[0]]
        }, GachaJson));
        var updated = await store.ReadCurrentAsync(DateTimeOffset.UtcNow, default);
        Check(updated.Pools.All(p => p.EventRules!.FourStarProbability == 0.1m &&
            p.Entries.Count(e => e.Rarity == 4) == 1), "One common-file edit must update every event without restart.");
        await File.WriteAllTextAsync(commonPath, originalCommon);
        var poolSource = new GachaPoolSource("CharPool_Furina", CommonPool: "character-event", FeaturedFiveStarKey: "furina");
        void Reject(GachaPoolSource pool, CharacterEventCommon invalid)
        {
            bool rejected = false;
            try { CharacterEventConfigBuilder.Build(pool, invalid); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Invalid event authoring data must be rejected.");
        }
        foreach (var rules in new[]
        {
            common.Rules with { FiveStarProbability = 0 }, common.Rules with { FourStarProbability = -1 },
            common.Rules with { FourStarProbability = 1 }, common.Rules with { FeaturedFiveStarProbability = 2 },
            common.Rules with { FiveStarPityCount = 0 }, common.Rules with { FourStarPityCount = 91 },
            common.Rules with { PityGroup = "../outside" }
        }) Reject(poolSource, common with { Rules = rules });
        Reject(poolSource, common with { ThreeStarWeapons = [] });
        Reject(poolSource, common with { FourStarWeapons = [] });
        Reject(poolSource, common with { FourStarWeapons = ["same", "same"] });
        Reject(poolSource, common with { StandardFiveStarCharacters = ["furina"] });
        Reject(poolSource, common with { ThreeStarWeapons = ["../outside"] });
        Reject(poolSource with { Entries = [] }, common);
        Reject(poolSource with { PityCount = 90 }, common);
        Reject(poolSource with { FeaturedFiveStarKey = "" }, common);
        string eventPath = Path.Combine(root, "gacha-CharPool_Furina.json");
        string originalEvent = await File.ReadAllTextAsync(eventPath);
        foreach (string invalid in new[]
        {
            originalEvent.Replace("character-event", "../outside"),
            originalEvent.Replace("character-event", "missing"),
            originalEvent.TrimEnd().TrimEnd('}') + ",\"unexpected\":true}"
        })
        {
            await File.WriteAllTextAsync(eventPath, invalid);
            bool rejected = false;
            try { await store.ReadCurrentAsync(DateTimeOffset.UtcNow, default); }
            catch (Exception e) when (e is InvalidDataException or IOException or JsonException) { rejected = true; }
            Check(rejected, "Invalid shared keys, missing files and unknown fields must fail configuration reads.");
        }
        await File.WriteAllTextAsync(eventPath, originalEvent);
        Check((await store.ReadCurrentAsync(DateTimeOffset.UtcNow, default)).Pools.Count == roles.Length, "Corrected event data recovers immediately.");
        Console.WriteLine("PASS: seven character events, common-file hot reload, weapon tiers and authoring validation.");
    }
}
