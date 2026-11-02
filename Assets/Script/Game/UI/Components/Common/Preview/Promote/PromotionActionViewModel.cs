using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UniRx;

internal sealed class PromotionActionViewModel : IDisposable
{
    internal readonly PromotionParticipant Participant;
    readonly ProgressionActionState state = new();
    internal ReactiveProperty<bool> Busy => state.Busy;
    internal readonly Subject<Unit> RequestSync = new();
    internal readonly Subject<string> RequestTip = new();
    internal readonly Subject<PromoteLevelResultData> PlayResult = new();
    internal bool Pending => CloudGameSaveSession.Current?.PendingPromote?.target.Identity == Participant.Target.Identity;
    internal bool HoldTargetView => Busy.Value || Pending;
    internal static List<ItemSlotViewModel> CreateMaterialSlots(IEnhanceable model)
    {
        var participant = PromotionParticipant.For(model);
        IEnumerable<PromoteMaterialCost> costs;
        if (PromotionParticipant.Rules != null)
            costs = participant.Materials.Select(m => new PromoteMaterialCost { materialKey = GameDatabase.ItemDatabase.GetItemByID(m.ItemId).key, count = m.Count });
        else
        {
            // Preserve the legacy local template; cloud targets always resolve their explicit rule binding.
            var definition = GameDatabase.PromoteDatabase.AllPromoteDefinitions.FirstOrDefault(d => d.characterKey == participant.Target.CharacterKey)
                ?? GameDatabase.PromoteDatabase.AllPromoteDefinitions.FirstOrDefault();
            costs = definition != null && participant.Rank < definition.rankRules.Count ? definition.rankRules[participant.Rank].materials : Array.Empty<PromoteMaterialCost>();
        }
        return costs.Select(cost =>
        {
            var definition = GameDatabase.ItemDatabase.GetItemByKey(cost.materialKey);
            return new ItemSlotViewModel(new MaterialItem(definition, GameContext.Instance.InventoryRepository.GetItemCount(definition.id), cost.count));
        }).ToList();
    }
    internal PromotionActionViewModel(PromotionParticipant participant)
    {
        Participant = participant;
        state.BindSending(GameSaveCoordinator.Instance.PromoteSending, () => RequestSync.OnNext(Unit.Default));
        if (PromotionParticipant.IsCloud) LoadRulesAsync(false).Forget();
        else state.Ready = true;
    }
    UniTask LoadRulesAsync(bool force) => state.LoadRulesAsync(
        async (session, token) =>
        {
            if (!session.HasPendingProgression && (force || session.PromoteConfig == null)) await session.LoadPromoteConfigAsync(token);
            return session.PromoteConfig != null;
        }, Participant.NotifyRulesChanged,
        error => RequestTip.OnNext("突破规则加载失败，请重试。" + error.Message));
    internal async UniTask SubmitAsync()
    {
        if (state.IsDisposed || Busy.Value) return;
        if (!state.Ready && !Pending) { await LoadRulesAsync(true); return; }
        if (!Pending)
        {
            if (Participant.Rank >= 6 || Participant.Level != PromotionParticipant.Rules.RankMaxLevels[Participant.Rank])
            { RequestTip.OnNext("尚未满足突破等级条件。"); return; }
            foreach (var material in Participant.Materials)
                if (GameContext.Instance.InventoryRepository.GetItemCount(material.ItemId) < material.Count)
                { RequestTip.OnNext("突破材料不足。"); return; }
            if (GameEconomy.Instance.GetCurrency(202) < Participant.GoldCost)
            { RequestTip.OnNext("金币不足，无法突破。"); return; }
        }
        try
        {
            await GameSaveCoordinator.Instance.PromoteAsync(Participant, (rank, level) =>
            {
                if (state.IsDisposed) return;
                var caps = PromotionParticipant.Rules.RankMaxLevels;
                PlayResult.OnNext(new PromoteLevelResultData(rank, rank + 1, level, caps[rank], caps[rank + 1], RarityConfig.GetColor(Participant.Rarity - 1)));
            });
        }
        catch (Exception error)
        {
            await state.ReportFailureAsync(error, () => Pending,
                "养成结果需要同步，请重新登录。", "突破结果待确认，请点击突破重试；也可重新登录恢复。",
                "突破未结算，已恢复本次预演。", "promote_config_changed", RequestTip.OnNext, () => LoadRulesAsync(true));
        }
        finally
        {
            if (!state.IsDisposed) { Participant.NotifyRulesChanged(); RequestSync.OnNext(Unit.Default); }
        }
    }
    public void Dispose()
    {
        // Only page-owned loading is cancelled. Journaled settlements outlive a closed page.
        state.Dispose();
        RequestSync.Dispose(); RequestTip.Dispose(); PlayResult.Dispose();
    }
}
