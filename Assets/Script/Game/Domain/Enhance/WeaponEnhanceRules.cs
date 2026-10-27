using System;
using System.IO;
using System.Linq;

namespace Game.Domain.Enhance
{
    // Both runtimes use integer tables so prediction does not depend on platform floating point math.
    internal sealed class WeaponEnhanceRules
    {
        public int SchemaVersion { get; set; }
        public int MaxMaterials { get; set; }
        public int GoldItemId { get; set; }
        public int GoldPerExp { get; set; }
        public int RecyclePercent { get; set; }
        public int[] RankMaxLevels { get; set; } = Array.Empty<int>();
        public int[] MaterialBaseExp { get; set; } = Array.Empty<int>();
        public int[][] ExperienceByRarity { get; set; } = Array.Empty<int[]>();
        public EnhanceWeaponDefinition[] Weapons { get; set; } = Array.Empty<EnhanceWeaponDefinition>();

        public void Validate()
        {
            if (SchemaVersion != 1 || MaxMaterials < 1 || MaxMaterials > 40 || GoldItemId != 202 ||
                GoldPerExp < 0 || GoldPerExp > 10000 || RecyclePercent < 0 || RecyclePercent > 100 ||
                RankMaxLevels == null || !RankMaxLevels.SequenceEqual(new[] { 20, 40, 50, 60, 70, 80, 90 }) ||
                MaterialBaseExp == null || MaterialBaseExp.Length != 5 || MaterialBaseExp.Any(e => e <= 0 || e > 10000000) ||
                ExperienceByRarity == null || ExperienceByRarity.Length != 5 || ExperienceByRarity.Any(row => row == null || row.Length != 90 ||
                    row.Any(e => e <= 0 || e > 10000000)) || Weapons == null || Weapons.Length == 0 ||
                Weapons.Any(w => w == null || w.ItemId <= 0 || w.Rarity < 0 || w.Rarity > 4 || w.Stars < 1 || w.Stars > 5) ||
                Weapons.Select(w => w.ItemId).Distinct().Count() != Weapons.Length)
                throw new InvalidDataException("Invalid weapon enhancement rules.");
        }

        public EnhanceWeaponDefinition Weapon(int itemId) => Weapons.SingleOrDefault(w => w.ItemId == itemId)
            ?? throw new InvalidDataException("Weapon is absent from enhancement catalog.");

        public int RequiredExp(int itemId, int level) => ExperienceByRarity[Weapon(itemId).Rarity][level - 1];

        public void ValidateState(int itemId, int level, int exp, int rank)
        {
            if (rank < 0 || rank >= RankMaxLevels.Length || level < 1 || level > RankMaxLevels[rank] ||
                exp < 0 || exp >= RequiredExp(itemId, level))
                throw new InvalidDataException("Invalid weapon enhancement state.");
        }

        public int MaterialExp(int itemId, int level, int exp, int rank)
        {
            ValidateState(itemId, level, exp, rank);
            long invested = exp;
            for (int i = 1; i < level; i++) invested += RequiredExp(itemId, i);
            long scaled = checked(invested * RecyclePercent);
            long recycled = scaled / 100;
            // Match Mathf.RoundToInt's midpoint-to-even recycling without floats.
            if (scaled % 100 > 50 || (scaled % 100 == 50 && recycled % 2 != 0)) recycled++;
            return checked(MaterialBaseExp[Weapon(itemId).Stars - 1] + (int)recycled);
        }

        public WeaponEnhancePrediction Preview(int itemId, int level, int exp, int rank, int addedExp)
        {
            ValidateState(itemId, level, exp, rank);
            if (addedExp < 0) throw new InvalidDataException("Negative enhancement experience.");
            int original = level;
            if (level >= RankMaxLevels[rank]) return new WeaponEnhancePrediction(level, exp, 0, 0);
            long remaining = checked((long)exp + addedExp);
            long gain = addedExp;
            while (remaining >= RequiredExp(itemId, level))
            {
                if (level >= RankMaxLevels[rank])
                {
                    gain -= remaining - (RequiredExp(itemId, level) - 1);
                    remaining = RequiredExp(itemId, level) - 1;
                    break;
                }
                remaining -= RequiredExp(itemId, level++);
            }
            return new WeaponEnhancePrediction(level, checked((int)remaining), checked((int)gain), level - original);
        }
    }

    internal sealed class EnhanceWeaponDefinition
    {
        public int ItemId { get; set; }
        public int Rarity { get; set; }
        public int Stars { get; set; }
    }

    internal readonly struct WeaponEnhancePrediction
    {
        internal readonly int Level, Exp, GainedExp, LevelsGained;
        internal WeaponEnhancePrediction(int level, int exp, int gainedExp, int levelsGained)
        { Level = level; Exp = exp; GainedExp = gainedExp; LevelsGained = levelsGained; }
    }
}
