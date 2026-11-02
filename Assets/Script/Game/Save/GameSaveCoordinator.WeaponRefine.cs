using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public sealed partial class GameSaveCoordinator
{
    internal readonly ReactiveProperty<bool> RefineSending = new(false);
    sealed class RefineProgression : ProgressionOperation
    {
        internal EquipItem Weapon;
        internal EquipItem[] Materials;
        internal int OldRefinement, Refinement, OldGold, Cost, Level, Exp, Rank;
        readonly long[] materialIds;
        Action<int, int> predicted;
        CloudWeaponRefineReply reply;

        internal RefineProgression(EquipItem weapon, long[] materialIds, Action<int, int> predicted, ReactiveProperty<bool> busy)
            : base(ProgressionKind.WeaponRefine, weapon.InstanceId.ToString(), busy,
                "请恢复原武器的精炼结果，或重新登录同步。", "精炼待确认期间本地进度发生变化，请重新登录同步。")
        { Weapon = weapon; this.materialIds = (long[])materialIds?.Clone(); this.predicted = predicted; }

        internal override async UniTask PrepareAsync(CloudGameSaveSession session, CancellationToken token)
        {
            if (session.RefineConfig == null) await session.LoadRefineConfigAsync(token);
            if (CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory) != session)
                throw new InvalidOperationException("精炼会话已经改变。");
            var rules = session.RefineConfig.rules;
            if (materialIds == null || materialIds.Length == 0 || materialIds.Length > rules.MaxMaterials ||
                materialIds.Distinct().Count() != materialIds.Length)
                throw new InvalidOperationException("请选择有效的精炼材料。");
            var inventory = GameContext.Instance.InventoryRepository;
            if (!inventory.TryGetEquip(Weapon.InstanceId, out var owned) || !ReferenceEquals(owned, Weapon))
                throw new InvalidOperationException("精炼武器已不在当前背包中。");
            Materials = materialIds.Select(id => inventory.TryGetEquip(id, out var item) && CanConsumeForRefinement(item, Weapon)
                ? item : throw new InvalidOperationException("只能消耗未装备的同名武器，且不能选择精炼目标。")).ToArray();
            Cost = rules.PreviewCost(Weapon.Id, Weapon.RefinementLevel, Materials.Length);
            OldGold = GameEconomy.Instance.GetCurrency(rules.GoldItemId);
            if (OldGold < Cost) throw new InvalidOperationException("金币不足，无法提交精炼。");
            OldRefinement = Weapon.RefinementLevel; Refinement = OldRefinement + Materials.Length;
            Level = Weapon.Level; Exp = Weapon.CurrentExp; Rank = Weapon.Rank;
            session.BeginRefinement(Weapon.InstanceId, materialIds);
        }

        internal override void ApplyPrediction()
        { Weapon.ApplyRefinementState(Refinement); ApplyGold(OldGold - Cost); }

        internal override void NotifyPrediction()
        {
            var notify = predicted; predicted = null; notify(OldRefinement, Refinement);
        }

        internal override async UniTask ResolveAsync(CloudGameSaveSession session, CancellationToken token)
        { reply = await session.ResolveRefinementAsync(token); }

        internal override void Confirm(CloudGameSaveSession session)
        { ApplyRefineReply(reply, this); session.AcceptRefinement(reply); }

        internal override bool MatchesLocal() => Applied && Weapon.RefinementLevel == Refinement && Weapon.Level == Level &&
            Weapon.CurrentExp == Exp && Weapon.Rank == Rank && GameEconomy.Instance.GetCurrency(202) == OldGold - Cost &&
            Materials.All(m => GameContext.Instance.InventoryRepository.TryGetEquip(m.InstanceId, out var current) && ReferenceEquals(current, m));
        internal override void Restore()
        {
            Weapon.ApplyRefinementState(OldRefinement); ApplyGold(OldGold);
        }
    }

    internal static bool CanConsumeForRefinement(InventoryItem material, EquipItem target) =>
        material is EquipItem && material.Id == target.Id && CanConsumeForEnhancement(material, target);

    internal async UniTask RefineWeaponAsync(EquipItem weapon, long[] materialIds, Action<int, int> predicted,
        CancellationToken token = default)
    {
        if (IsProgressionRunning) throw new InvalidOperationException("武器养成正在处理中。");
        var session = CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory);
        if (!session.IsReady || session.HasConflict || session.HasPendingGacha || session.HasPendingEnhance)
            throw new InvalidOperationException("云端精炼尚未就绪，请先确认上一次操作。");
        await RunProgressionAsync(new RefineProgression(weapon, materialIds, predicted, RefineSending), token);
    }

    static void ApplyRefineReply(CloudWeaponRefineReply reply, RefineProgression prediction)
    {
        if (reply.state.revision != reply.operation.committedRevision)
            throw new InvalidDataException("精炼后云端已有新进度，请重新登录读取完整存档。");
        var save = JsonUtility.FromJson<GameSaveData>(reply.state.saveJson);
        var target = save?.inventory?.equips?.SingleOrDefault(e => e.instanceId == prediction.Weapon.InstanceId);
        var gold = save?.currencies?.items?.SingleOrDefault(c => c.itemId == 202);
        var op = reply.operation;
        if (save?.version != 8 || target == null || target.itemId != prediction.Weapon.Id ||
            target.level != prediction.Level || target.exp != prediction.Exp || target.rank != prediction.Rank ||
            target.refinementLevel != op.refinementLevel || op.oldRefinementLevel != prediction.OldRefinement ||
            gold == null || gold.amount < 0 || gold.amount != prediction.OldGold - op.goldCost ||
            prediction.Materials.Any(m => save.inventory.equips.Any(e => e.instanceId == m.InstanceId)))
            throw new InvalidDataException("精炼回执与存档内容不一致，请重新登录恢复。");
        // A valid accepted result is authoritative even when its cost differs from the prediction.
        prediction.Weapon.ApplyRefinementState(target.refinementLevel); ApplyGold(gold.amount);
        foreach (var material in prediction.Materials) GameContext.Instance.InventoryRepository.RemoveItem(material);
    }
}
