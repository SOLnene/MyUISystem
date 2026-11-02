public partial class EquipItem
{
    internal void ApplyPromotionState(int rank, int exp)
    {
        RankSystem = new RankSystem(rank);
        LevelSystem = new LevelSystem(Level, exp, (int)ItemRarity);
        RefreshBaseStats(); rankRP.Value = rank; levelRP.SetValueAndForceNotify(Level); expRP.Value = exp;
    }
    internal void NotifyPromotionRulesChanged() => rankRP.SetValueAndForceNotify(Rank);
}
