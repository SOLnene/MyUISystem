using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

internal sealed class CharacterEventGacha
{
    readonly Dictionary<string, GachaEventPitySaveData> states = new Dictionary<string, GachaEventPitySaveData>(StringComparer.Ordinal);
    readonly Func<float> random;

    internal CharacterEventGacha(Func<float> nextRandom = null) { random = nextRandom ?? (() => UnityEngine.Random.value); }

    internal GachaResult Draw(int count, GachaDefinition pool)
    {
        var rules = pool.EventRules;
        if (!states.TryGetValue(rules.pityGroup, out var state))
        {
            state = new GachaEventPitySaveData { groupKey = rules.pityGroup };
            states.Add(rules.pityGroup, state);
        }
        var results = new List<GachaEntry>();
        for (int i = 0; i < count; i++)
        {
            state.fiveStarCount = Math.Min(state.fiveStarCount, pool.pityCount - 1) + 1;
            state.fourStarCount = Math.Min(state.fourStarCount, rules.fourStarPityCount - 1) + 1;
            float roll = random();
            // Five-star outcomes take precedence when both guarantees are due and satisfy four-star-or-above pity.
            if (state.fiveStarCount >= pool.pityCount || roll < rules.fiveStarProbability)
            {
                bool featured = state.featuredFiveStarGuaranteed || random() < rules.featuredFiveStarProbability;
                var entry = featured ? pool.entries.Find(e => e.entryKey == rules.featuredFiveStarKey && e.rarity == 5)
                    : Pick(pool.entries, 5, rules.featuredFiveStarKey);
                state.fiveStarCount = 0;
                state.fourStarCount = 0;
                state.featuredFiveStarGuaranteed = !featured;
                results.Add(entry);
            }
            else if (state.fourStarCount >= rules.fourStarPityCount || roll < rules.fiveStarProbability + rules.fourStarProbability)
            {
                state.fourStarCount = 0;
                results.Add(Pick(pool.entries, 4));
            }
            else results.Add(Pick(pool.entries, 3));
        }
        return new GachaResult(results);
    }

    GachaEntry Pick(IReadOnlyList<GachaEntry> entries, int rarity, string excludedCharacter = null)
    {
        int total = 0;
        foreach (var entry in entries)
            if (entry.rarity == rarity && entry.entryKey != excludedCharacter) total = checked(total + entry.weight);
        if (total == 0) throw new InvalidDataException("活动卡池缺少对应星级的奖励。");
        int roll = Math.Min(total - 1, (int)(random() * total));
        foreach (var entry in entries)
        {
            if (entry.rarity != rarity || entry.entryKey == excludedCharacter) continue;
            if (roll < entry.weight) return entry;
            roll -= entry.weight;
        }
        throw new InvalidDataException("活动卡池权重无效。");
    }

    internal void ExportTo(GachaSaveData data)
    {
        foreach (var state in states.Values)
            data.eventPityCounters.Add(new GachaEventPitySaveData
            {
                groupKey = state.groupKey, fiveStarCount = state.fiveStarCount,
                fourStarCount = state.fourStarCount, featuredFiveStarGuaranteed = state.featuredFiveStarGuaranteed
            });
    }

    internal void Import(IReadOnlyList<GachaEventPitySaveData> saved)
    {
        states.Clear();
        if (saved == null) return;
        foreach (var state in saved)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.groupKey)) continue;
            states[state.groupKey] = new GachaEventPitySaveData
            {
                groupKey = state.groupKey, fiveStarCount = Math.Max(0, state.fiveStarCount),
                fourStarCount = Math.Max(0, state.fourStarCount), featuredFiveStarGuaranteed = state.featuredFiveStarGuaranteed
            };
        }
    }
}
