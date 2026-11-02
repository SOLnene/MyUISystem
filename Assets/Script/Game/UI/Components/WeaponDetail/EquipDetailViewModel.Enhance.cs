using System;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public partial class EquipDetailViewModel
{
    readonly ProgressionActionState enhanceState = new();
    internal ReactiveProperty<bool> enhanceBusy => enhanceState.Busy;
    internal readonly Subject<Unit> requestSyncEnhancement = new();
    internal readonly Subject<string> requestEnhanceTip = new();
    static bool IsCloudEnhancement => CloudGameSaveSession.UsesServerSettlement && CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory);

    void InitializeEnhancement()
    {
        enhanceState.BindSending(GameSaveCoordinator.Instance.EnhanceSending, () =>
        {
            currentWeaponVM.Value.SyncFromModel();
            UpdateEnhanceAvailability(); requestSyncEnhancement.OnNext(Unit.Default);
        });
        if (IsCloudEnhancement) LoadEnhanceRulesAsync().Forget();
        else { enhanceState.Ready = true; UpdateEnhanceAvailability(); }
    }

    void UpdateEnhanceAvailability()
    {
        var session = IsCloudEnhancement ? CloudGameSaveSession.Current : null;
        bool pending = session?.HasPendingEnhance ?? false;
        // A predicted cap cannot enable promotion; keep the enhancement retry button until settlement is confirmed.
        currentWeaponVM.Value.needBreak.Value = currentWeaponVM.Value.Model.NeedBreak() && !pending;
        bottomVM.canEnhance.Value = !enhanceBusy.Value && !refineBusy.Value && !(session?.HasPendingRefine ?? false) &&
            (session == null || !session.HasConflict) &&
            (pending ? session.PendingEnhance.weaponInstanceId == currentWeaponVM.Value.Model.InstanceId :
                enhanceState.Ready && enhanceVM.previewExp.Value > 0);
        UpdatePromotionAvailability();
    }

    UniTask LoadEnhanceRulesAsync() => enhanceState.LoadRulesAsync(
        async (session, token) =>
        {
            if (!session.HasPendingEnhance) await session.LoadEnhanceConfigAsync(token);
            return session.EnhanceConfig != null;
        },
        () =>
        {
            currentWeaponVM.Value.Model.NotifyEnhanceRulesChanged();
            enhanceVM.rightBottomVM.RefreshSelection(); currentWeaponVM.Value.SyncFromModel();
        },
        error => requestEnhanceTip.OnNext("强化规则加载失败，请重新打开武器页面。" + error.Message));

    async UniTask SubmitCloudEnhancementAsync()
    {
        if (enhanceState.IsDisposed || enhanceBusy.Value || (!enhanceState.Ready && !(CloudGameSaveSession.Current?.HasPendingEnhance ?? false))) return;
        var weapon = currentWeaponVM.Value;
        try
        {
            await GameSaveCoordinator.Instance.EnhanceWeaponAsync(weapon.Model, enhanceVM.rightBottomVM.SelectedMaterialIds,
                (oldLevel, oldExp) =>
                {
                    if (enhanceState.IsDisposed) return;
                    weapon.SyncFromModel();
                    UpdateEnhanceAvailability();
                    requestPlayEnhanceResult.OnNext(new EnhanceResultData(oldLevel, weapon.level.Value,
                        (float)oldExp / weapon.Model.EnhanceExpRequired(oldLevel), GetExpProgress(weapon),
                        weapon.level.Value - oldLevel, weapon.needBreak.Value, RarityConfig.GetColor(weapon.Model.ItemRarity)));
                    enhanceVM.ClearSelectedMaterials(); requestCloseItemSelectPanel.OnNext(Unit.Default);
                });
        }
        catch (Exception error)
        {
            await enhanceState.ReportFailureAsync(error, () => CloudGameSaveSession.Current?.HasPendingEnhance ?? false,
                "养成结果需要同步，请重新登录。", "强化结果待确认，请点击强化重试；也可重新登录恢复。",
                "强化未结算，已恢复本次预演。", "enhance_config_changed", requestEnhanceTip.OnNext, LoadEnhanceRulesAsync);
        }
        finally
        {
            if (!enhanceState.IsDisposed)
            { weapon.SyncFromModel(); UpdateEnhanceAvailability(); requestSyncEnhancement.OnNext(Unit.Default); }
        }
    }

    void DisposeEnhancement()
    {
        // Closing the view cancels config loading, never a settlement whose journal is already durable.
        enhanceState.Dispose();
        requestSyncEnhancement.Dispose(); requestEnhanceTip.Dispose();
    }
}
