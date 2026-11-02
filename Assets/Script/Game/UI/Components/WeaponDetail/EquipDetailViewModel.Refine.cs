using System;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public partial class EquipDetailViewModel
{
    readonly ProgressionActionState refineState = new();
    internal ReactiveProperty<bool> refineBusy => refineState.Busy;

    void InitializeRefinement()
    {
        refineState.BindSending(GameSaveCoordinator.Instance.RefineSending, () =>
        {
            currentWeaponVM.Value.SyncFromModel(); UpdateRefineAvailability();
            requestSyncEnhancement.OnNext(Unit.Default);
        });
        currentWeaponVM.Where(w => w != null).Subscribe(_ => UpdateRefineAvailability()).AddTo(disposables);
        if (IsCloudEnhancement) LoadRefineRulesAsync().Forget();
        else { refineState.Ready = true; UpdateRefineAvailability(); }
    }

    void UpdateRefineAvailability()
    {
        bool pending = IsCloudEnhancement && (CloudGameSaveSession.Current?.HasPendingRefine ?? false);
        // Keep the normal submit button available to recover a predicted maximum whose receipt is still unknown.
        bottomVM.isRefineMaxed.Value = !pending && currentWeaponVM.Value.IsRefineMaxed();
        UpdateEnhanceAvailability();
    }

    UniTask LoadRefineRulesAsync() => refineState.LoadRulesAsync(
        async (session, token) =>
        {
            if (!session.HasPendingRefine) await session.LoadRefineConfigAsync(token);
            return session.RefineConfig != null;
        },
        () =>
        {
            refineVM.ClearSelectedMaterials(); currentWeaponVM.Value.SyncFromModel();
        },
        error => requestEnhanceTip.OnNext("精炼规则加载失败，请重新打开武器页面。" + error.Message));

    async UniTask SubmitCloudRefinementAsync()
    {
        var session = CloudGameSaveSession.Current;
        if (refineState.IsDisposed || refineBusy.Value || enhanceBusy.Value || (!refineState.Ready && !(session?.HasPendingRefine ?? false))) return;
        if (!(session?.HasPendingRefine ?? false) && !refineVM.CanApplyRefine()) return;
        var weapon = currentWeaponVM.Value;
        try
        {
            await GameSaveCoordinator.Instance.RefineWeaponAsync(weapon.Model, refineVM.SelectedMaterialIds,
                (oldRefinement, refinement) =>
                {
                    if (refineState.IsDisposed) return;
                    weapon.SyncFromModel(); UpdateRefineAvailability();
                    refineVM.ClearSelectedMaterials(); requestCloseItemSelectPanel.OnNext(Unit.Default);
                    requestPlayRefineResult.OnNext(new RefineResultData(oldRefinement, refinement, weapon.IsRefineMaxed()));
                });
        }
        catch (Exception error)
        {
            await refineState.ReportFailureAsync(error, () => CloudGameSaveSession.Current?.HasPendingRefine ?? false,
                "养成结果需要同步，请重新登录。", "精炼结果待确认，请点击精炼重试；也可重新登录恢复。",
                "精炼未结算，已恢复本次预演。", "refine_config_changed", requestEnhanceTip.OnNext, LoadRefineRulesAsync);
        }
        finally
        {
            if (!refineState.IsDisposed)
            { weapon.SyncFromModel(); UpdateRefineAvailability(); requestSyncEnhancement.OnNext(Unit.Default); }
        }
    }

    void DisposeRefinement()
    {
        // Closing the page cancels only rule loading; the durable settlement continues independently.
        refineState.Dispose();
    }
}
