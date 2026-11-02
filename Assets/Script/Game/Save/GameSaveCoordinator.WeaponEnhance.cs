using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Enhance;
using UniRx;
using UnityEngine;

public sealed partial class GameSaveCoordinator
{
    internal readonly ReactiveProperty<bool> EnhanceSending = new(false);
    sealed class EnhanceProgression : ProgressionOperation
    {
        internal EquipItem Weapon;
        internal EquipItem[] Materials;
        internal int OldLevel, OldExp, OldGold, Cost;
        internal WeaponEnhancePrediction Result;
        readonly long[] materialIds;
        Action<int, int> predicted;
        CloudWeaponEnhanceReply reply;

        internal EnhanceProgression(EquipItem weapon, long[] materialIds, Action<int, int> predicted, ReactiveProperty<bool> busy)
            : base(ProgressionKind.WeaponEnhance, weapon.InstanceId.ToString(), busy,
                "请恢复原武器的强化结果，或重新登录同步。", "强化待确认期间本地进度发生变化，请重新登录同步。")
        { Weapon = weapon; this.materialIds = (long[])materialIds?.Clone(); this.predicted = predicted; }

        internal override async UniTask PrepareAsync(CloudGameSaveSession session, CancellationToken token)
        {
            if (session.EnhanceConfig == null) await session.LoadEnhanceConfigAsync(token);
            if (CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory) != session)
                throw new InvalidOperationException("强化会话已经改变。");
            var rules = session.EnhanceConfig.rules;
            if (materialIds == null || materialIds.Length == 0 || materialIds.Length > rules.MaxMaterials ||
                materialIds.Distinct().Count() != materialIds.Length)
                throw new InvalidOperationException("请选择有效的强化材料。");
            var inventory = GameContext.Instance.InventoryRepository;
            if (!inventory.TryGetEquip(Weapon.InstanceId, out var owned) || !ReferenceEquals(owned, Weapon))
                throw new InvalidOperationException("强化武器已不在当前背包中。");
            Materials = materialIds.Select(id => inventory.TryGetEquip(id, out var item) && CanConsumeForEnhancement(item, Weapon)
                ? item : throw new InvalidOperationException("不能消耗强化目标或已装备的武器。")).ToArray();
            int exp = 0;
            foreach (var material in Materials) exp = checked(exp + rules.MaterialExp(material.Id, material.Level, material.CurrentExp, material.Rank));
            Result = rules.Preview(Weapon.Id, Weapon.Level, Weapon.CurrentExp, Weapon.Rank, exp);
            if (Result.GainedExp <= 0) throw new InvalidOperationException("武器已达到当前等级上限。");
            Cost = checked(Result.GainedExp * rules.GoldPerExp); OldGold = GameEconomy.Instance.GetCurrency(rules.GoldItemId);
            if (OldGold < Cost) throw new InvalidOperationException("金币不足，无法提交强化。");
            OldLevel = Weapon.Level; OldExp = Weapon.CurrentExp;
            session.BeginEnhancement(Weapon.InstanceId, materialIds);
        }

        internal override void ApplyPrediction()
        { Weapon.ApplyEnhancementState(Result.Level, Result.Exp); ApplyGold(OldGold - Cost); }

        internal override void NotifyPrediction()
        {
            // Retrying needs only model state; do not retain a closed ViewModel through its animation callback.
            var notify = predicted; predicted = null; notify(OldLevel, OldExp);
        }

        internal override async UniTask ResolveAsync(CloudGameSaveSession session, CancellationToken token)
        { reply = await session.ResolveEnhancementAsync(token); }

        internal override void Confirm(CloudGameSaveSession session)
        { ApplyEnhanceReply(reply, this); session.AcceptEnhancement(reply); }

        internal override bool MatchesLocal() => Applied && Weapon.Level == Result.Level && Weapon.CurrentExp == Result.Exp &&
            GameEconomy.Instance.GetCurrency(202) == OldGold - Cost &&
            Materials.All(m => GameContext.Instance.InventoryRepository.TryGetEquip(m.InstanceId, out var current) && ReferenceEquals(m, current));

        internal override void Restore()
        {
            Weapon.ApplyEnhancementState(OldLevel, OldExp);
            ApplyGold(OldGold);
        }
    }

    internal static bool CanConsumeForEnhancement(InventoryItem material, EquipItem target) => material is EquipItem &&
        material.InstanceId != target.InstanceId && !GameContext.Instance.CharacterRepository.Characters
            .Any(c => c.CurrentEquipRP.Value?.InstanceId == material.InstanceId);

    internal async UniTask EnhanceWeaponAsync(EquipItem weapon, long[] materialIds, Action<int, int> predicted,
        CancellationToken token = default)
    {
        if (IsProgressionRunning) throw new InvalidOperationException("强化正在处理中。");
        var session = CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory);
        if (!session.IsReady || session.HasConflict || session.HasPendingGacha || session.HasPendingRefine)
            throw new InvalidOperationException("云端强化尚未就绪，请先确认上一次操作。");
        await RunProgressionAsync(new EnhanceProgression(weapon, materialIds, predicted, EnhanceSending), token);
    }

    static void ApplyGold(int amount) => GameEconomy.Instance.ApplyStoreBalances(new CurrencySaveData
        { items = { new CurrencyAmountSaveData(202, amount) } });

    static void ApplyEnhanceReply(CloudWeaponEnhanceReply reply, EnhanceProgression prediction)
    {
        if (reply.state.revision != reply.operation.committedRevision)
            throw new InvalidDataException("强化后云端已有新进度，请重新登录读取完整存档。");
        var save = JsonUtility.FromJson<GameSaveData>(reply.state.saveJson);
        var target = save?.inventory?.equips?.SingleOrDefault(e => e.instanceId == prediction.Weapon.InstanceId);
        var gold = save?.currencies?.items?.SingleOrDefault(c => c.itemId == 202);
        var progress = save?.achievements?.progress?.SingleOrDefault(p => p.progressKey == AchievementProgressKeys.WeaponEnhance);
        var op = reply.operation;
        if (save?.version != 8 || target == null || target.itemId != prediction.Weapon.Id || target.rank != prediction.Weapon.Rank ||
            target.refinementLevel != prediction.Weapon.RefinementLevel || target.level != op.level || target.exp != op.exp ||
            target.level < 1 || target.exp < 0 || gold == null || gold.amount != prediction.OldGold - op.goldCost || gold.amount < 0 ||
            progress == null || progress.value < 1 || prediction.Materials.Any(m => save.inventory.equips.Any(e => e.instanceId == m.InstanceId)))
            throw new InvalidDataException("强化回执与存档内容不一致，请重新登录恢复。");
        // Accepted results may differ from the preview; they remain successful and replace the predicted values.
        prediction.Weapon.ApplyEnhancementState(target.level, target.exp); ApplyGold(gold.amount);
        foreach (var material in prediction.Materials) GameContext.Instance.InventoryRepository.RemoveItem(material);
        var achievements = AchievementProgressService.Instance;
        achievements.AddProgress(AchievementProgressKeys.WeaponEnhance,
            Math.Max(0, progress.value - achievements.GetProgress(AchievementProgressKeys.WeaponEnhance)));
    }
}
