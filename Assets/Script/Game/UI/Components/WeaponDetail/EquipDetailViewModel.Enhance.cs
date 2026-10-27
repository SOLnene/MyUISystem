using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public partial class EquipDetailViewModel
{
    internal readonly ReactiveProperty<bool> enhanceBusy = new(false);
    internal readonly Subject<Unit> requestSyncEnhancement = new();
    internal readonly Subject<string> requestEnhanceTip = new();
    readonly CancellationTokenSource enhanceLoadCancellation = new();
    bool enhanceRulesReady, enhanceDisposed, loadingEnhanceRules;
    static bool IsCloudEnhancement => CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory);

    void InitializeEnhancement()
    {
        GameSaveCoordinator.Instance.EnhanceSending.Subscribe(_ =>
        {
            if (enhanceDisposed) return;
            currentWeaponVM.Value.SyncFromModel();
            UpdateEnhanceAvailability(); requestSyncEnhancement.OnNext(Unit.Default);
        }).AddTo(disposables);
        if (IsCloudEnhancement) LoadEnhanceRulesAsync().Forget();
        else { enhanceRulesReady = true; UpdateEnhanceAvailability(); }
    }

    void UpdateEnhanceAvailability()
    {
        var session = IsCloudEnhancement ? CloudGameSaveSession.Current : null;
        bool pending = session?.HasPendingEnhance ?? false;
        // A predicted cap cannot enable promotion; keep the enhancement retry button until settlement is confirmed.
        currentWeaponVM.Value.needBreak.Value = currentWeaponVM.Value.Model.NeedBreak() && !pending;
        enhanceBusy.Value = loadingEnhanceRules || GameSaveCoordinator.Instance.EnhanceSending.Value;
        bottomVM.canEnhance.Value = !enhanceBusy.Value && (session == null || !session.HasConflict) &&
            (pending ? session.PendingEnhance.weaponInstanceId == currentWeaponVM.Value.Model.InstanceId :
                enhanceRulesReady && enhanceVM.previewExp.Value > 0);
    }

    async UniTask LoadEnhanceRulesAsync()
    {
        loadingEnhanceRules = true; UpdateEnhanceAvailability();
        try
        {
            var session = CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory);
            if (!session.HasPendingEnhance) await session.LoadEnhanceConfigAsync(enhanceLoadCancellation.Token);
            if (enhanceDisposed) return;
            enhanceRulesReady = session.EnhanceConfig != null;
            currentWeaponVM.Value.Model.NotifyEnhanceRulesChanged();
            enhanceVM.rightBottomVM.RefreshSelection(); currentWeaponVM.Value.SyncFromModel();
        }
        catch (OperationCanceledException) when (enhanceLoadCancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            enhanceRulesReady = false;
            if (!enhanceDisposed) requestEnhanceTip.OnNext("强化规则加载失败，请重新打开武器页面。" + error.Message);
        }
        finally
        {
            loadingEnhanceRules = false;
            if (!enhanceDisposed) { UpdateEnhanceAvailability(); requestSyncEnhancement.OnNext(Unit.Default); }
        }
    }

    async UniTask SubmitCloudEnhancementAsync()
    {
        if (enhanceBusy.Value || (!enhanceRulesReady && !(CloudGameSaveSession.Current?.HasPendingEnhance ?? false))) return;
        var weapon = currentWeaponVM.Value;
        try
        {
            await GameSaveCoordinator.Instance.EnhanceWeaponAsync(weapon.Model, enhanceVM.rightBottomVM.SelectedMaterialIds,
                (oldLevel, oldExp) =>
                {
                    if (enhanceDisposed) return;
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
            if (!enhanceDisposed)
            {
                bool pending = CloudGameSaveSession.Current?.HasPendingEnhance ?? false;
                requestEnhanceTip.OnNext(pending ? "强化结果待确认，请点击强化重试；也可重新登录恢复。" :
                    error is CloudSaveServerException server ? "强化未结算，已恢复本次预演。" + server.Code : error.Message);
                if (error is CloudSaveServerException changed && changed.Code == "enhance_config_changed")
                    await LoadEnhanceRulesAsync();
            }
        }
        finally
        {
            if (!enhanceDisposed)
            { weapon.SyncFromModel(); UpdateEnhanceAvailability(); requestSyncEnhancement.OnNext(Unit.Default); }
        }
    }

    void DisposeEnhancement()
    {
        // Closing the view cancels config loading, never a settlement whose journal is already durable.
        enhanceDisposed = true; enhanceLoadCancellation.Cancel(); enhanceLoadCancellation.Dispose();
        enhanceBusy.Dispose(); requestSyncEnhancement.Dispose(); requestEnhanceTip.Dispose();
    }
}
