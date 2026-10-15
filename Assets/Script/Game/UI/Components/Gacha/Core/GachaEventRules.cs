using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

[Serializable]
internal sealed class GachaEventRules
{
    public string pityGroup = null;
    public float fiveStarProbability = 0;
    public float fourStarProbability = 0;
    public int fourStarPityCount = 0;
    public string featuredFiveStarKey = null;
    public float featuredFiveStarProbability = 0;

    internal void Validate(int fiveStarPityCount, IReadOnlyList<GachaEntry> entries)
    {
        if (string.IsNullOrEmpty(pityGroup) || !Regex.IsMatch(pityGroup, @"\A[A-Za-z0-9_-]{1,64}\z") ||
            !PositiveProbability(fiveStarProbability) || !PositiveProbability(fourStarProbability) ||
            fiveStarProbability + fourStarProbability >= 1 || fourStarPityCount < 1 || fourStarPityCount > fiveStarPityCount ||
            !PositiveProbability(featuredFiveStarProbability) || string.IsNullOrEmpty(featuredFiveStarKey) ||
            !entries.Any(e => e.rarity == 5 && e.entryType == GachaEntryType.Character && e.entryKey == featuredFiveStarKey) ||
            !entries.Any(e => e.rarity == 4) || !entries.Any(e => e.rarity == 3) ||
            entries.Any(e => e.rarity < 3 || (e.rarity == 5 && e.entryType != GachaEntryType.Character)) ||
            (featuredFiveStarProbability < 1 && !entries.Any(e => e.rarity == 5 && e.entryKey != featuredFiveStarKey)))
            throw new InvalidDataException("服务器返回的角色活动池规则或物品分档无效。");
    }

    internal bool SharesPolicyWith(GachaEventRules other, int pityCount, int otherPityCount) =>
        pityCount == otherPityCount && fiveStarProbability == other.fiveStarProbability &&
        fourStarProbability == other.fourStarProbability && fourStarPityCount == other.fourStarPityCount &&
        featuredFiveStarProbability == other.featuredFiveStarProbability;

    static bool PositiveProbability(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0 && value <= 1;
}
