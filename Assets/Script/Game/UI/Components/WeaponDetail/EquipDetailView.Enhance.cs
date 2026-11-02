using UniRx;

public partial class EquipDetailView
{
    bool enhanceSyncRequested;

    void BindEnhancementState()
    {
        equipDetailVm.refineBusy.Subscribe(_ =>
        {
            SetInputBlocked(inputBlockCount > 0);
            SyncEnhancementDisplay();
        }).AddTo(disposable);
        equipDetailVm.requestEnhanceTip.Subscribe(message => UIManager.Instance.Open(UIType.TipView, message)).AddTo(disposable);
        equipDetailVm.enhanceBusy.Subscribe(_ =>
        {
            SetInputBlocked(inputBlockCount > 0);
            SyncEnhancementDisplay();
        }).AddTo(disposable);
        equipDetailVm.requestSyncEnhancement.Subscribe(_ => SyncEnhancementDisplay()).AddTo(disposable);
        equipDetailVm.promotion.Busy.Subscribe(_ =>
        {
            SetInputBlocked(inputBlockCount > 0);
            SyncEnhancementDisplay();
        }).AddTo(disposable);
    }

    void SyncEnhancementDisplay()
    {
        if (equipDetailVm == null || isClosing) return;
        if (isPlayingResultFlow || isSwitchingTab) { enhanceSyncRequested = true; return; }
        enhanceSyncRequested = false;
        if (currentTabIndex == (int)WeaponDetailTab.Refine)
        {
            refinePanelView.Refresh(); bottomView.Refresh();
            if (equipDetailVm.refineBusy.Value)
            { refinePanelView.ShowRefineProcessing(); bottomView.ShowRefineBottomProcessing(); }
            return;
        }
        if (currentTabIndex != (int)WeaponDetailTab.Enhance) return;
        // A correction can arrive during an EXP tween. Refresh only after the existing animation stops writing the bar.
        enhancePanelView.Refresh(); bottomView.Refresh();
        if (equipDetailVm.enhanceBusy.Value)
        { enhancePanelView.ShowEnhanceProcessing(); bottomView.ShowEnhanceBottomProcessing(); }
        if (equipDetailVm.promotion.Busy.Value && equipDetailVm.currentWeaponVM.Value.needBreak.Value)
        { enhancePanelView.ShowPromoteProcessing(); bottomView.ShowPromoteBottomProcessing(); }
    }
}
