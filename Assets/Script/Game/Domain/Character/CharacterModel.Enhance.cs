using Game.Domain.Enhance;

namespace Game.Domain.Character
{
    public partial class CharacterModel
    {
        internal CharacterEnhanceRules EnhanceRules => CloudGameSaveSession.Current?.IsReady == true &&
            CloudGameSaveSession.Current.Source == GameContext.Instance.ActiveSaveDirectory
            ? CloudGameSaveSession.Current.CharacterEnhanceConfig?.rules : null;

        internal int EnhanceExpRequired(int level) => EnhanceRules?.RequiredExp(Definition.key, level) ?? LevelSystem.GetExpRequired(level);

        internal LevelPreview PreviewEnhancement(int providedExp)
        {
            if (EnhanceRules == null) return LevelSystem.GetPreviewWithExp(providedExp, GetCurrentMaxLevel());
            // Level/EXP notifications are sequential; calculate from the already replaced numeric state, never a partial RP update.
            var value = EnhanceRules.Preview(Definition.key, LevelSystem.Level, LevelSystem.CurrentExp, RankRP.Value, providedExp);
            return new LevelPreview { finalLevel = value.Level, finalExp = value.Exp,
                levelUpCount = value.Level - LevelSystem.Level, cappedExpGain = value.GainedExp };
        }

        internal void ApplyEnhancementState(int level, int exp)
        {
            // Preserve the character and its reactive properties so existing page bindings remain attached.
            LevelSystem = new LevelSystem(level, exp, Definition.rarity);
            RefreshBaseStats(); levelRP.Value = level; expRP.Value = exp;
        }

        internal void NotifyEnhanceRulesChanged()
        { levelRP.SetValueAndForceNotify(LevelRP.Value); expRP.SetValueAndForceNotify(ExpRP.Value); }
    }
}
