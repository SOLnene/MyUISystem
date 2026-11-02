using System;
using System.Collections.Generic;

namespace Game.Domain.Enhance
{
    // Publish one complete calculation; display projections must not feed clipped EXP back into the material input.
    internal sealed class EnhancementPreviewData
    {
        internal readonly int CurrentLevel, CurrentExp, CurrentRequiredExp, PreviewRequiredExp;
        internal readonly int ProvidedExp, DisplayExp, GoldCost;
        internal readonly LevelPreview Level;
        internal readonly IReadOnlyList<StatPreviewData> Stats;
        internal int AcceptedExp => Level.cappedExpGain;
        internal int OverflowExp => Math.Max(0, ProvidedExp - AcceptedExp);

        internal EnhancementPreviewData(int currentLevel, int currentExp, int currentRequiredExp, int previewRequiredExp,
            int providedExp, int displayExp, int goldCost, LevelPreview level, IReadOnlyList<StatPreviewData> stats)
        {
            CurrentLevel = currentLevel; CurrentExp = currentExp;
            CurrentRequiredExp = currentRequiredExp; PreviewRequiredExp = previewRequiredExp;
            ProvidedExp = providedExp; DisplayExp = displayExp; GoldCost = goldCost; Level = level; Stats = stats;
        }
    }
}
