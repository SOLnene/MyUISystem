using System;
using System.Linq;
using Game.Domain.Character;
using Game.Domain.Promote;

internal sealed class PromotionParticipant
{
    internal readonly IEnhanceable Model;
    internal readonly IPromotable Promotable;
    internal readonly PromotionTarget Target;
    internal readonly int ItemId, Rarity;
    internal CharacterModel Character => Model as CharacterModel;
    internal EquipItem Weapon => Model as EquipItem;
    internal int Level => Model.LevelSystem.Level;
    internal int Exp => Model.LevelSystem.CurrentExp;
    internal int Rank => Promotable.RankSystem.CurrentRank;
    internal string OtherState => Character != null ? Character.TalentLevel + ":" + Character.TalentTokenCount + ":" + (Character.CurrentEquipRP.Value?.InstanceId ?? 0)
        : Weapon.Id + ":" + Weapon.RefinementLevel;
    internal static PromotionRules Rules => CloudGameSaveSession.Current?.IsReady == true &&
        CloudGameSaveSession.Current.Source == GameContext.Instance.ActiveSaveDirectory ? CloudGameSaveSession.Current.PromoteConfig?.rules : null;
    internal static bool IsCloud => CloudGameSaveSession.UsesServerSettlement && CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory);

    PromotionParticipant(IEnhanceable model)
    {
        Model = model; Promotable = (IPromotable)model;
        if (model is CharacterModel character)
        { Target = new PromotionTarget { Kind = "character", CharacterKey = character.Definition.key }; Rarity = character.Definition.rarity; }
        else if (model is EquipItem weapon)
        { Target = new PromotionTarget { Kind = "weapon", WeaponInstanceId = weapon.InstanceId }; ItemId = weapon.Id; Rarity = weapon.Stars; }
        else throw new ArgumentException("Unsupported promotion target.");
    }
    internal static PromotionParticipant For(IEnhanceable model) => new(model);
    internal bool IsOwned => Character != null ? ReferenceEquals(GameContext.Instance.CharacterRepository.GetByKey(Target.CharacterKey), Character)
        : GameContext.Instance.InventoryRepository.TryGetEquip(Target.WeaponInstanceId, out var owned) && ReferenceEquals(owned, Weapon);
    int PresentationRank => CloudGameSaveSession.Current?.PendingPromote?.target.Identity == Target.Identity ? Math.Max(0, Rank - 1) : Rank;
    internal int GoldCost => PresentationRank >= 6 ? 0 : Rules?.Resolve(Target, ItemId, PresentationRank).GoldCost ?? GrowthCostFormula.GetPromoteGoldCost(PresentationRank, Rarity);
    internal int NextLevelCap => Rules?.RankMaxLevels[Math.Min(6, PresentationRank + 1)] ?? new RankSystem(PresentationRank).GetNextRankMaxLevel();
    internal PromotionMaterial[] Materials
    {
        get
        {
            if (PresentationRank >= 6) return Array.Empty<PromotionMaterial>();
            if (Rules != null) return Rules.Resolve(Target, ItemId, PresentationRank).Rule.Materials;
            var definition = GameDatabase.PromoteDatabase.AllPromoteDefinitions.FirstOrDefault(d => d.characterKey == Target.CharacterKey)
                ?? GameDatabase.PromoteDatabase.AllPromoteDefinitions.FirstOrDefault();
            return definition != null && PresentationRank < definition.rankRules.Count
                ? definition.rankRules[PresentationRank].materials.Select(cost => new PromotionMaterial
                    { ItemId = GameDatabase.ItemDatabase.GetItemByKey(cost.materialKey).id, Count = cost.count }).ToArray()
                : Array.Empty<PromotionMaterial>();
        }
    }
    internal void Apply(int rank, int exp)
    {
        if (Character != null) Character.ApplyPromotionState(rank, exp);
        else Weapon.ApplyPromotionState(rank, exp);
    }
    internal void NotifyRulesChanged()
    {
        if (Character != null) Character.NotifyPromotionRulesChanged();
        else Weapon.NotifyPromotionRulesChanged();
    }
}
