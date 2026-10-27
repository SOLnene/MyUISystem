using Game.Domain.Enhance;

public partial class EquipItem
{
    WeaponEnhanceRules EnhanceRules => CloudGameSaveSession.Current?.IsReady == true &&
        CloudGameSaveSession.Current.Source == GameContext.Instance.ActiveSaveDirectory
        ? CloudGameSaveSession.Current.EnhanceConfig?.rules : null;

    internal int EnhanceExpRequired(int level) => EnhanceRules?.RequiredExp(Id, level) ?? LevelSystem.GetExpRequired(level);

    internal void NotifyEnhanceRulesChanged()
    {
        // Values may be unchanged while the remotely configured EXP denominator has changed.
        levelRP.SetValueAndForceNotify(Level); expRP.SetValueAndForceNotify(CurrentExp);
    }

    internal LevelPreview PreviewEnhancement(int addedExp)
    {
        if (EnhanceRules == null) return LevelSystem.GetPreviewWithExp(addedExp, GetCurrentMaxLevel());
        var result = EnhanceRules.Preview(Id, Level, CurrentExp, Rank, addedExp);
        return new LevelPreview { finalLevel = result.Level, finalExp = result.Exp,
            levelUpCount = result.LevelsGained, cappedExpGain = result.GainedExp };
    }

    internal void ApplyEnhancementState(int level, int exp)
    {
        // Keep the item and reactive properties alive; only replace its numeric level state.
        LevelSystem = new LevelSystem(level, exp, (int)ItemRarity);
        RefreshBaseStats(); levelRP.Value = Level; expRP.Value = CurrentExp;
    }
}
