using System;
using System.Collections.Generic;

namespace Game.Domain.Enhance
{
    internal static class ExpBookRules
    {
        internal static readonly IReadOnlyList<string> Keys = Array.AsReadOnly(new[]
            { "expbook_small", "expbook_medium", "expbook_large" });

        internal static CharacterEnhanceRules CurrentRules => CloudGameSaveSession.Current?.IsReady == true &&
            CloudGameSaveSession.Current.Source == GameContext.Instance.ActiveSaveDirectory
            ? CloudGameSaveSession.Current.CharacterEnhanceConfig?.rules : null;

        internal static int Experience(string key) => CurrentRules != null
            ? Array.Find(CurrentRules.Books, book => book.Key == key)?.Experience ?? 0 : key switch
        {
            "expbook_small" => 100,
            "expbook_medium" => 500,
            "expbook_large" => 2500,
            _ => 0
        };

        internal static int GoldCost(string key) => CurrentRules != null
            ? Array.Find(CurrentRules.Books, book => book.Key == key)?.GoldCost ?? 0
            : GrowthCostFormula.GetEnhanceGoldCost(Experience(key));
    }
}
