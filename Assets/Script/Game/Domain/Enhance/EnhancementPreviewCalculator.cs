using System;
using Game.Domain.Character;
using UniRx;

namespace Game.Domain.Enhance
{
    internal static class EnhancementPreviewCalculator
    {
        internal static IObservable<EnhancementPreviewData> Observe(IEnhanceable model, IReadOnlyReactiveProperty<int> materialExp,
            Func<int> goldCost = null) => Observable.CombineLatest(
            model.ChangeRP.Merge(model.ExpRP.AsUnitObservable()), materialExp,
            (_, exp) => ForTarget(model, exp, goldCost?.Invoke() ?? 0));

        internal static EnhancementPreviewData ForTarget(IEnhanceable model, int providedExp, int goldCost)
        {
            if (model is EquipItem weapon) return ForWeapon(weapon, providedExp, out _);
            var level = model.LevelSystem;
            var result = level.Level >= model.GetCurrentMaxLevel()
                ? new LevelPreview { finalLevel = level.Level, finalExp = level.CurrentExp }
                : model is CharacterModel target ? target.PreviewEnhancement(providedExp)
                    : level.GetPreviewWithExp(providedExp, model.GetCurrentMaxLevel());
            var stats = model is CharacterModel character
                ? character.BuildStatPreview(result) : model.GetStatPreview(providedExp);
            // Character books display their supplied EXP; weapon previews retain their existing accepted-EXP display.
            return new(level.Level, level.CurrentExp,
                model is CharacterModel current ? current.EnhanceExpRequired(level.Level) : level.GetExpRequired(),
                model is CharacterModel next ? next.EnhanceExpRequired(result.finalLevel) : level.GetExpRequired(result.finalLevel),
                providedExp, providedExp, goldCost, result, stats);
        }

        internal static EnhancementPreviewData ForWeapon(EquipItem weapon, int providedExp, out EquipPreview equipment)
        {
            var result = weapon.Level >= weapon.GetCurrentMaxLevel()
                ? new LevelPreview { finalLevel = weapon.Level, finalExp = weapon.CurrentExp }
                : weapon.PreviewEnhancement(providedExp);
            equipment = weapon.BuildEquipPreview(result);
            return new(weapon.Level, weapon.CurrentExp, weapon.EnhanceExpRequired(weapon.Level),
                weapon.EnhanceExpRequired(result.finalLevel), providedExp, result.cappedExpGain, equipment.costGold,
                result, weapon.BuildStatPreview(equipment));
        }
    }
}
