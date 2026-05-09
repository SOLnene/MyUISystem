using UnityEngine;

public static class PromoteCostFormula
{
    static readonly int[] RankBaseCosts =
    {
        20000,
        40000,
        60000,
        80000,
        100000,
        120000
    };

    static readonly float[] RarityMultipliers =
    {
        0.45f,
        0.6f,
        0.8f,
        1f,
        1.25f
    };

    public static int GetGoldCost(int currentRank, int rarity)
    {
        int rankIndex = Mathf.Clamp(currentRank, 0, RankBaseCosts.Length - 1);
        int rarityIndex = Mathf.Clamp(rarity - 1, 0, RarityMultipliers.Length - 1);
        return Mathf.RoundToInt(RankBaseCosts[rankIndex] * RarityMultipliers[rarityIndex]);
    }
}
