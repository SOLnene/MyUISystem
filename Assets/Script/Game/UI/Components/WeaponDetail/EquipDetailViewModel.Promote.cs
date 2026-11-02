using UniRx;

public partial class EquipDetailViewModel
{
    internal PromotionActionViewModel promotion;
    readonly CompositeDisposable promotionSubscriptions = new();
    void InitializePromotion()
    {
        currentWeaponVM.Where(weapon => weapon != null).Subscribe(weapon =>
        {
            promotionSubscriptions.Clear(); promotion?.Dispose();
            promotion = new PromotionActionViewModel(PromotionParticipant.For(weapon.Model));
            promotion.PlayResult.Subscribe(result =>
            {
                weapon.SyncFromModel(); UpdatePromotionAvailability(); requestPlayPromoteResult.OnNext(result);
            }).AddTo(promotionSubscriptions);
            promotion.RequestTip.Subscribe(requestEnhanceTip.OnNext).AddTo(promotionSubscriptions);
            promotion.Busy.Subscribe(_ =>
            {
                UpdatePromotionAvailability(); requestSyncEnhancement.OnNext(Unit.Default);
            }).AddTo(promotionSubscriptions);
            promotion.RequestSync.Subscribe(_ =>
            {
                weapon.SyncFromModel(); UpdateEnhanceAvailability(); UpdatePromotionAvailability();
                requestSyncEnhancement.OnNext(Unit.Default);
            }).AddTo(promotionSubscriptions);
            UpdatePromotionAvailability();
        }).AddTo(disposables);
    }
    void UpdatePromotionAvailability()
    {
        if (promotion == null) return;
        var weapon = currentWeaponVM.Value;
        bool waitingEnhance = CloudGameSaveSession.Current?.HasPendingEnhance ?? false;
        weapon.needBreak.Value = (weapon.Model.NeedBreak() && !waitingEnhance) || promotion.Pending;
        bottomVM.canBreakout.Value = weapon.needBreak.Value;
        if (weapon.needBreak.Value)
        { enhanceVM.previewCost.Value = promotion.Participant.GoldCost; bottomVM.totalCostGold.Value = enhanceVM.previewCost.Value; }
    }
    void DisposePromotion()
    { promotionSubscriptions.Dispose(); promotion?.Dispose(); }
}
