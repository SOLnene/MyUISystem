using Cysharp.Threading.Tasks;
using UniRx;

namespace Game.UI.Components.CharacterDetail
{
    public partial class CharacterDetailView
    {
        void BindCharacterEnhancementState()
        {
            vm.promoteViewmodel.promotion.RequestTip.Subscribe(message => UIManager.Instance.Open(UIType.TipView, message)).AddTo(characterDisposable);
            vm.promoteViewmodel.promotion.Busy.Subscribe(_ =>
            {
                SetResultInputBlocked(currentOperation == DetailOperation.PlayingResult);
                SyncCharacterEnhancementDisplay();
            }).AddTo(characterDisposable);
            vm.promoteViewmodel.promotion.RequestSync.Subscribe(_ => SyncCharacterEnhancementDisplay()).AddTo(characterDisposable);
            vm.enhanceViewmodel.requestEnhanceTip
                .Subscribe(message => UIManager.Instance.Open(UIType.TipView, message)).AddTo(characterDisposable);
            vm.enhanceViewmodel.enhancementBusy.Subscribe(_ =>
            {
                SetResultInputBlocked(currentOperation == DetailOperation.PlayingResult);
                SyncCharacterEnhancementDisplay();
            }).AddTo(characterDisposable);
            vm.enhanceViewmodel.requestSyncEnhancement.Subscribe(_ => SyncCharacterEnhancementDisplay()).AddTo(characterDisposable);
        }

        void SyncCharacterEnhancementDisplay()
        {
            if (vm == null || isClosing || currentOperation != DetailOperation.None ||
                (!enhancePanelView.gameObject.activeSelf && !promotePanelView.gameObject.activeSelf)) return;
            // Completion can arrive while the original animation still owns the bar. CompleteOperation calls us again afterwards.
            enhancePanelView.RefreshEnhancement();
            if (vm.promoteViewmodel.promotion.Busy.Value)
            {
                if (promotePanelView.gameObject.activeSelf) promotePanelView.ShowPromoteProcessing();
                return;
            }
            if (vm.enhanceViewmodel.enhancementBusy.Value)
            {
                if (enhancePanelView.gameObject.activeSelf) enhancePanelView.ShowEnhanceProcessing();
                return;
            }
            bool showUpgrade = (vm.model.LevelRP.Value < vm.model.GetCurrentMaxLevel() || vm.enhanceViewmodel.HasPendingEnhancement) && !vm.promoteViewmodel.promotion.Pending;
            if (showUpgrade == enhancePanelView.gameObject.activeSelf)
            {
                if (showUpgrade) enhancePanelView.ShowEnhanceNormal(false);
                else { promotePanelView.RefreshMaterials(); promotePanelView.ShowPromoteNormal(false); }
                return;
            }
            RefreshAuthoritativeUpgradePageAsync().Forget();
        }

        async UniTask RefreshAuthoritativeUpgradePageAsync()
        {
            int version = BeginOperation(DetailOperation.PlayingResult);
            try
            {
                if (vm.enhanceViewmodel.HasPendingEnhancement)
                {
                    promotePanelView.gameObject.SetActive(false);
                    await enhancePanelView.ShowPanel(false);
                }
                else await RefreshUpgradeOrPromotePanelAsync(false);
            }
            finally { CompleteOperation(version); }
        }
    }
}
