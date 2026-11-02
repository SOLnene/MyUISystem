namespace Game.Domain.Character
{
    public partial class CharacterModel
    {
        internal void ApplyPromotionState(int rank, int exp)
        {
            // Publish only after rank, EXP and derived attributes agree; preserve existing observable references.
            RankSystem = new RankSystem(rank);
            LevelSystem = new LevelSystem(LevelSystem.Level, exp, Definition.rarity);
            RefreshBaseStats(); rankRP.Value = rank; levelRP.SetValueAndForceNotify(LevelSystem.Level); expRP.Value = exp;
        }
        internal void NotifyPromotionRulesChanged() => rankRP.SetValueAndForceNotify(RankSystem.CurrentRank);
    }
}
