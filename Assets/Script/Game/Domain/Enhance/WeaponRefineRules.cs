using System;
using System.IO;
using System.Linq;

namespace Game.Domain.Enhance
{
    // Integer price tables are shared by prediction and settlement; clients never send a price or result.
    internal sealed class WeaponRefineRules
    {
        public int SchemaVersion { get; set; }
        public int GoldItemId { get; set; }
        public int MaxMaterials { get; set; }
        public int RefinementCap { get; set; }
        public int[][] CostsByStars { get; set; } = Array.Empty<int[]>();
        public RefineWeaponDefinition[] Weapons { get; set; } = Array.Empty<RefineWeaponDefinition>();

        public void Validate()
        {
            if (SchemaVersion != 1 || GoldItemId != 202 || MaxMaterials < 1 || MaxMaterials > 4 ||
                RefinementCap < 2 || RefinementCap > 5 || CostsByStars == null || CostsByStars.Length != 5 ||
                CostsByStars.Any(row => row == null || row.Length != RefinementCap - 1 || row.Any(c => c < 0 || c > 100000000)) ||
                Weapons == null || Weapons.Length == 0 || Weapons.Any(w => w == null || w.ItemId <= 0 || w.Stars < 1 || w.Stars > 5) ||
                Weapons.Select(w => w.ItemId).Distinct().Count() != Weapons.Length)
                throw new InvalidDataException("Invalid weapon refinement rules.");
        }

        public RefineWeaponDefinition Weapon(int itemId) => Weapons.SingleOrDefault(w => w.ItemId == itemId)
            ?? throw new InvalidDataException("Weapon is absent from refinement catalog.");

        public int Cost(int itemId, int refinementLevel)
        {
            if (refinementLevel < 1 || refinementLevel >= RefinementCap)
                throw new InvalidDataException("Invalid refinement level for price lookup.");
            return CostsByStars[Weapon(itemId).Stars - 1][refinementLevel - 1];
        }

        public int PreviewCost(int itemId, int refinementLevel, int materialCount)
        {
            Weapon(itemId);
            if (refinementLevel < 1 || refinementLevel >= RefinementCap || materialCount < 1 ||
                materialCount > MaxMaterials || materialCount > RefinementCap - refinementLevel)
                throw new InvalidDataException("Invalid refinement material count or level.");
            int cost = 0;
            for (int i = 0; i < materialCount; i++) cost = checked(cost + Cost(itemId, refinementLevel + i));
            return cost;
        }
    }

    internal sealed class RefineWeaponDefinition
    {
        public int ItemId { get; set; }
        public int Stars { get; set; }
    }
}
