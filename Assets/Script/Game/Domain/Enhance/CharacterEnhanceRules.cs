using System;
using System.IO;
using System.Linq;

namespace Game.Domain.Enhance
{
    internal sealed class CharacterEnhanceRules
    {
        public int SchemaVersion { get; set; }
        public int GoldItemId { get; set; }
        public int MaxBookCount { get; set; }
        public int[] RankMaxLevels { get; set; } = Array.Empty<int>();
        public int[][] ExperienceByRarity { get; set; } = Array.Empty<int[]>();
        public EnhanceCharacterDefinition[] Characters { get; set; } = Array.Empty<EnhanceCharacterDefinition>();
        public EnhanceBookDefinition[] Books { get; set; } = Array.Empty<EnhanceBookDefinition>();

        public void Validate()
        {
            if (SchemaVersion != 1 || GoldItemId != 202 || MaxBookCount < 1 || MaxBookCount > 100000 ||
                RankMaxLevels == null || !RankMaxLevels.SequenceEqual(new[] { 20, 40, 50, 60, 70, 80, 90 }) ||
                ExperienceByRarity == null || ExperienceByRarity.Length != 5 ||
                ExperienceByRarity.Any(row => row == null || row.Length != 90 || row.Any(x => x < 1 || x > 10000000)) ||
                Characters == null || Characters.Length == 0 || Characters.Any(c => c == null || string.IsNullOrWhiteSpace(c.Key) || c.Key.Length > 128 || c.Rarity < 1 || c.Rarity > 5) ||
                Characters.Select(c => c.Key).Distinct(StringComparer.Ordinal).Count() != Characters.Length ||
                Books == null || Books.Length != 3 || Books.Any(b => b == null || b.Experience < 1 || b.Experience > 10000000 || b.GoldCost < 0 || b.GoldCost > 10000000) ||
                !Books.OrderBy(b => b.ItemId).Select(b => b.ItemId).SequenceEqual(new[] { 501, 502, 503 }) ||
                !Books.OrderBy(b => b.ItemId).Select(b => b.Key).SequenceEqual(new[] { "expbook_small", "expbook_medium", "expbook_large" }))
                throw new InvalidDataException("Invalid character enhancement rules.");
        }

        public EnhanceCharacterDefinition Character(string key) => Characters.SingleOrDefault(c => c.Key == key)
            ?? throw new InvalidDataException("Character is absent from enhancement catalog.");
        public EnhanceBookDefinition Book(int itemId) => Books.SingleOrDefault(b => b.ItemId == itemId)
            ?? throw new ArgumentException("Unknown experience book.");
        public int RequiredExp(string key, int level) => ExperienceByRarity[Character(key).Rarity - 1][level - 1];

        public void ValidateState(string key, int level, int exp, int rank)
        {
            Character(key);
            if (rank < 0 || rank >= RankMaxLevels.Length || level < 1 || level > RankMaxLevels[rank] ||
                exp < 0 || exp >= RequiredExp(key, level)) throw new InvalidDataException("Invalid character enhancement state.");
        }

        public (int Experience, int GoldCost) BookTotals(CharacterEnhanceBookSelection[] books)
        {
            if (books == null || books.Length == 0 || books.Length > 3 ||
                books.Any(b => b == null || b.Count <= 0) || books.Select(b => b.ItemId).Distinct().Count() != books.Length)
                throw new ArgumentException("Invalid experience book selection.");
            int count = 0, exp = 0, cost = 0;
            foreach (var amount in books)
            {
                var book = Book(amount.ItemId);
                count = checked(count + amount.Count);
                if (count > MaxBookCount) throw new ArgumentException("Experience book selection exceeds limit.");
                exp = checked(exp + checked(amount.Count * book.Experience));
                cost = checked(cost + checked(amount.Count * book.GoldCost));
            }
            // Books are indivisible: the whole selected quantity is consumed and charged even when EXP is capped.
            return (exp, cost);
        }

        public CharacterEnhancePrediction Preview(string key, int level, int exp, int rank, int providedExp)
        {
            ValidateState(key, level, exp, rank);
            if (providedExp < 0) throw new ArgumentException("Negative experience.");
            if (level >= RankMaxLevels[rank]) return new(level, exp, 0);
            long remaining = checked((long)exp + providedExp), gain = providedExp;
            while (remaining >= RequiredExp(key, level))
            {
                if (level >= RankMaxLevels[rank])
                {
                    gain -= remaining - (RequiredExp(key, level) - 1);
                    remaining = RequiredExp(key, level) - 1;
                    break;
                }
                remaining -= RequiredExp(key, level++);
            }
            return new(level, checked((int)remaining), checked((int)gain));
        }
    }

    internal sealed class EnhanceCharacterDefinition
    {
        public string Key { get; set; } = string.Empty;
        public int Rarity { get; set; }
    }
    internal sealed class EnhanceBookDefinition
    {
        public int ItemId { get; set; }
        public string Key { get; set; } = string.Empty;
        public int Experience { get; set; }
        public int GoldCost { get; set; }
    }
    internal sealed class CharacterEnhanceBookSelection
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }
    internal readonly struct CharacterEnhancePrediction
    {
        internal readonly int Level, Exp, GainedExp;
        internal CharacterEnhancePrediction(int level, int exp, int gainedExp)
        { Level = level; Exp = exp; GainedExp = gainedExp; }
    }
}
