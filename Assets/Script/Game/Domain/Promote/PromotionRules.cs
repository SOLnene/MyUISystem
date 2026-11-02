using System;
using System.IO;
using System.Linq;

namespace Game.Domain.Promote
{
    internal sealed class PromotionTarget
    {
        public string Kind { get; set; } = string.Empty;
        public string CharacterKey { get; set; } = string.Empty;
        public long WeaponInstanceId { get; set; }
        internal bool IsValid => Kind == "character" ? !string.IsNullOrWhiteSpace(CharacterKey) && CharacterKey.Length <= 128 && WeaponInstanceId == 0
            : Kind == "weapon" && WeaponInstanceId > 0 && string.IsNullOrEmpty(CharacterKey);
        internal string Identity => Kind + ":" + (Kind == "character" ? CharacterKey : WeaponInstanceId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
    internal sealed class PromotionMaterial
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }
    internal sealed class PromotionRankRule
    {
        public int Rank { get; set; }
        public int[] GoldByRarity { get; set; } = Array.Empty<int>();
        public PromotionMaterial[] Materials { get; set; } = Array.Empty<PromotionMaterial>();
    }
    internal sealed class PromotionRuleSet
    {
        public string Key { get; set; } = string.Empty;
        public PromotionRankRule[] Ranks { get; set; } = Array.Empty<PromotionRankRule>();
    }
    internal sealed class PromotionCharacter
    {
        public string Key { get; set; } = string.Empty;
        public int Rarity { get; set; }
        public string RuleKey { get; set; } = string.Empty;
    }
    internal sealed class PromotionWeapon
    {
        public int ItemId { get; set; }
        public int Rarity { get; set; }
        public string RuleKey { get; set; } = string.Empty;
    }
    internal sealed class PromotionRules
    {
        public int SchemaVersion { get; set; }
        public int GoldItemId { get; set; }
        public int[] RankMaxLevels { get; set; } = Array.Empty<int>();
        public PromotionRuleSet[] RuleSets { get; set; } = Array.Empty<PromotionRuleSet>();
        public PromotionCharacter[] Characters { get; set; } = Array.Empty<PromotionCharacter>();
        public PromotionWeapon[] Weapons { get; set; } = Array.Empty<PromotionWeapon>();
        public void Validate()
        {
            if (SchemaVersion != 1 || GoldItemId != 202 || RankMaxLevels == null || !RankMaxLevels.SequenceEqual(new[]{20,40,50,60,70,80,90}) ||
                RuleSets == null || RuleSets.Length == 0 || RuleSets.Any(s => s == null || string.IsNullOrWhiteSpace(s.Key) || s.Ranks == null || s.Ranks.Length != 6 ||
                    s.Ranks.Where((r,i) => r == null || r.Rank != i || r.GoldByRarity == null || r.GoldByRarity.Length != 5 || r.GoldByRarity.Any(g => g < 0) ||
                        r.Materials == null || r.Materials.Length == 0 || r.Materials.Any(m => m == null || m.ItemId <= 0 || m.Count <= 0 || m.Count > 1000000) ||
                        r.Materials.Select(m => m.ItemId).Distinct().Count() != r.Materials.Length).Any()) ||
                RuleSets.Select(s => s.Key).Distinct().Count() != RuleSets.Length || Characters == null || Characters.Length == 0 || Weapons == null || Weapons.Length == 0 ||
                Characters.Any(c => c == null || string.IsNullOrWhiteSpace(c.Key) || c.Rarity < 1 || c.Rarity > 5 || !RuleSets.Any(s => s.Key == c.RuleKey)) ||
                Weapons.Any(w => w == null || w.ItemId <= 0 || w.Rarity < 1 || w.Rarity > 5 || !RuleSets.Any(s => s.Key == w.RuleKey)) ||
                Characters.Select(c => c.Key).Distinct().Count() != Characters.Length || Weapons.Select(w => w.ItemId).Distinct().Count() != Weapons.Length)
                throw new InvalidDataException("Invalid promotion configuration.");
        }
        public (PromotionRankRule Rule, int GoldCost) Resolve(PromotionTarget target, int itemId, int rank)
        {
            if (!target.IsValid || rank < 0 || rank >= 6) throw new ArgumentException("Invalid promotion target or rank.");
            string key; int rarity;
            if (target.Kind == "character")
            { var c = Characters.SingleOrDefault(c => c.Key == target.CharacterKey) ?? throw new InvalidDataException("Unknown promotion character."); key = c.RuleKey; rarity = c.Rarity; }
            else
            { var w = Weapons.SingleOrDefault(w => w.ItemId == itemId) ?? throw new InvalidDataException("Unknown promotion weapon."); key = w.RuleKey; rarity = w.Rarity; }
            var rule = RuleSets.Single(s => s.Key == key).Ranks[rank];
            return (rule, rule.GoldByRarity[rarity - 1]);
        }
    }
}
