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
    EnhancePrediction pendingEnhancePrediction;

    sealed class EnhancePrediction
    {
        internal EquipItem Weapon;
        internal EquipItem[] Materials;
        internal int OldLevel, OldExp, OldGold, Cost;
        internal WeaponEnhancePrediction Result;
        internal uint DirtyRevision;
        internal bool Applied;

        internal bool MatchesLocal() => Applied && Weapon.Level == Result.Level && Weapon.CurrentExp == Result.Exp &&
            GameEconomy.Instance.GetCurrency(202) == OldGold - Cost &&
            Materials.All(m => GameContext.Instance.InventoryRepository.TryGetEquip(m.InstanceId, out var current) && ReferenceEquals(m, current));

        internal void Restore()
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
        if (EnhanceSending.Value) throw new InvalidOperationException("强化正在处理中。");
        EnhanceSending.Value = true;
        CloudGameSaveSession session = null;
        string source = GameContext.Instance.ActiveSaveDirectory;
        bool ownsFlush = false;
        try
        {
            while (isFlushing) { token.ThrowIfCancellationRequested(); await UniTask.Yield(); }
            session = CloudGameSaveSession.RequireSession(source);
            session.RequireNoPendingStore();
            if (!session.IsReady || session.HasConflict || session.HasPendingGacha)
                throw new InvalidOperationException("云端强化尚未就绪，请先确认上一次操作。");
            if (!session.HasPendingEnhance)
            {
                while (dirty || session.HasPendingSave)
                    if (!await FlushCloudAsync(token) && LastCloudError != null) throw LastCloudError;
                isFlushing = true; ownsFlush = true;
                if (session.EnhanceConfig == null) await session.LoadEnhanceConfigAsync(token);
                if (CloudGameSaveSession.RequireSession(source) != session) throw new InvalidOperationException("强化会话已经改变。");
                var rules = session.EnhanceConfig.rules;
                if (materialIds == null || materialIds.Length == 0 || materialIds.Length > rules.MaxMaterials ||
                    materialIds.Distinct().Count() != materialIds.Length)
                    throw new InvalidOperationException("请选择有效的强化材料。");
                var inventory = GameContext.Instance.InventoryRepository;
                if (!inventory.TryGetEquip(weapon.InstanceId, out var owned) || !ReferenceEquals(owned, weapon))
                    throw new InvalidOperationException("强化武器已不在当前背包中。");
                var materials = materialIds.Select(id => inventory.TryGetEquip(id, out var item) && CanConsumeForEnhancement(item, weapon)
                    ? item : throw new InvalidOperationException("不能消耗强化目标或已装备的武器。")).ToArray();
                int exp = 0;
                foreach (var material in materials) exp = checked(exp + rules.MaterialExp(material.Id, material.Level, material.CurrentExp, material.Rank));
                var result = rules.Preview(weapon.Id, weapon.Level, weapon.CurrentExp, weapon.Rank, exp);
                if (result.GainedExp <= 0) throw new InvalidOperationException("武器已达到当前等级上限。");
                int cost = checked(result.GainedExp * rules.GoldPerExp), gold = GameEconomy.Instance.GetCurrency(rules.GoldItemId);
                if (gold < cost) throw new InvalidOperationException("金币不足，无法提交强化。");
                pendingEnhancePrediction = new EnhancePrediction { Weapon = weapon, Materials = materials,
                    OldLevel = weapon.Level, OldExp = weapon.CurrentExp, OldGold = gold, Cost = cost, Result = result };
                session.BeginEnhancement(weapon.InstanceId, materialIds);
                weapon.ApplyEnhancementState(result.Level, result.Exp);
                ApplyGold(gold - cost); pendingEnhancePrediction.Applied = true;
                predicted(pendingEnhancePrediction.OldLevel, pendingEnhancePrediction.OldExp);
                // UI observers may mark the prediction dirty. It is covered by the journal, never by a normal save.
                dirty = false; pendingEnhancePrediction.DirtyRevision = dirtyRevision;
            }
            else if (session.PendingEnhance.weaponInstanceId != weapon.InstanceId || pendingEnhancePrediction == null)
                throw new InvalidOperationException("请恢复原武器的强化结果，或重新登录同步。");

            isFlushing = true; ownsFlush = true;
            var reply = await session.ResolveEnhancementAsync(token);
            await UniTask.SwitchToMainThread(token);
            var prediction = pendingEnhancePrediction;
            if (CloudGameSaveSession.RequireSession(source) != session || prediction.DirtyRevision != dirtyRevision || !prediction.MatchesLocal())
            {
                session.RejectGachaApplication();
                throw new InvalidOperationException("强化待确认期间本地进度发生变化，请重新登录同步。");
            }
            try
            {
                ApplyEnhanceReply(reply, prediction);
                session.AcceptEnhancement(reply); pendingEnhancePrediction = null;
                dirty = false; LastCloudError = null;
            }
            catch { session.RejectGachaApplication(); throw; }
        }
        catch (Exception error)
        {
            await UniTask.SwitchToMainThread();
            if (session != null && !session.HasPendingEnhance && pendingEnhancePrediction != null)
            {
                // Only a definitive rejection rolls back our own still-isolated prediction.
                var prediction = pendingEnhancePrediction;
                if (CloudGameSaveSession.Current == session && prediction.DirtyRevision == dirtyRevision && prediction.MatchesLocal())
                { prediction.Restore(); dirty = false; }
                else if (prediction.Applied) session.RejectGachaApplication();
                pendingEnhancePrediction = null;
            }
            LastCloudError = error; throw;
        }
        finally
        {
            if (ownsFlush) isFlushing = false;
            saveAt = Time.realtimeSinceStartup + SaveDelay; EnhanceSending.Value = false;
        }
    }

    static void ApplyGold(int amount) => GameEconomy.Instance.ApplyStoreBalances(new CurrencySaveData
        { items = { new CurrencyAmountSaveData(202, amount) } });

    static void ApplyEnhanceReply(CloudWeaponEnhanceReply reply, EnhancePrediction prediction)
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
