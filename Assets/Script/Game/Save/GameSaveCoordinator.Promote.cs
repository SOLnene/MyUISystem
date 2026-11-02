using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Promote;
using UniRx;
using UnityEngine;

public sealed partial class GameSaveCoordinator
{
    internal readonly ReactiveProperty<bool> PromoteSending = new(false);
    sealed class PromoteProgression : ProgressionOperation
    {
        internal readonly PromotionParticipant Participant;
        internal int OldRank, OldExp, Level, OldGold, Cost;
        internal int[] OldCounts;
        internal PromotionMaterial[] Materials;
        string otherState;
        Action<int, int> predicted;
        internal CloudPromoteReply Reply;
        internal PromoteProgression(PromotionParticipant participant, Action<int, int> predicted, ReactiveProperty<bool> busy)
            : base(ProgressionKind.Promote, participant.Target.Identity, busy, "请恢复原目标的突破结果，或重新登录同步。", "突破待确认期间本地进度发生变化，请重新登录同步。")
        { Participant = participant; this.predicted = predicted; }
        internal override async UniTask PrepareAsync(CloudGameSaveSession session, CancellationToken token)
        {
            if (session.PromoteConfig == null) await session.LoadPromoteConfigAsync(token);
            if (CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory) != session || !Participant.IsOwned)
                throw new InvalidOperationException("突破目标不属于当前会话。");
            OldRank = Participant.Rank; OldExp = Participant.Exp; Level = Participant.Level; otherState = Participant.OtherState;
            if (OldRank >= 6 || Level != session.PromoteConfig.rules.RankMaxLevels[OldRank]) throw new InvalidOperationException("尚未满足突破等级条件。");
            var rule = session.PromoteConfig.rules.Resolve(Participant.Target, Participant.ItemId, OldRank);
            Cost = rule.GoldCost; Materials = rule.Rule.Materials.Select(m => new PromotionMaterial { ItemId = m.ItemId, Count = m.Count }).ToArray();
            OldCounts = Materials.Select(m => GameContext.Instance.InventoryRepository.GetItemCount(m.ItemId)).ToArray();
            if (Materials.Where((m, i) => m.Count > OldCounts[i]).Any()) throw new InvalidOperationException("突破材料不足。");
            OldGold = GameEconomy.Instance.GetCurrency(202);
            if (OldGold < Cost) throw new InvalidOperationException("金币不足，无法突破。");
            session.BeginPromotion(Participant.Target);
        }
        internal override void ApplyPrediction()
        { Participant.Apply(OldRank + 1, Participant.Character != null ? OldExp : 0); ApplyGold(OldGold - Cost); }
        internal override void NotifyPrediction()
        { var notify = predicted; predicted = null; notify?.Invoke(OldRank, Level); }
        internal override async UniTask ResolveAsync(CloudGameSaveSession session, CancellationToken token)
        { Reply = await session.ResolvePromotionAsync(token); }
        internal override bool MatchesLocal() => Applied && Participant.IsOwned && Participant.Level == Level && Participant.Rank == OldRank + 1 &&
            Participant.Exp == (Participant.Character != null ? OldExp : 0) && Participant.OtherState == otherState &&
            GameEconomy.Instance.GetCurrency(202) == OldGold - Cost && Materials.Where((m, i) => GameContext.Instance.InventoryRepository.GetItemCount(m.ItemId) != OldCounts[i]).Any() == false;
        internal override void Restore() { Participant.Apply(OldRank, OldExp); ApplyGold(OldGold); }
        internal override void Confirm(CloudGameSaveSession session)
        {
            var op = Reply.operation;
            if (Reply.state.revision != op.committedRevision) throw new InvalidDataException("突破后云端已有新进度，请重新登录同步。");
            var save = JsonUtility.FromJson<GameSaveData>(Reply.state.saveJson);
            var gold = save?.currencies?.items?.SingleOrDefault(c => c.itemId == 202);
            if (save?.version != 8 || save.inventory?.stacks == null || gold == null || gold.amount < 0 || gold.amount != OldGold - op.goldCost ||
                op.oldRank != OldRank || op.oldExp != OldExp || op.level != Level || op.rank != OldRank + 1 || op.exp != (Participant.Character != null ? OldExp : 0))
                throw new InvalidDataException("突破回执与存档不一致。");
            if (Participant.Character != null)
            {
                var character = save.characters.characters.SingleOrDefault(c => c.characterKey == Participant.Target.CharacterKey);
                if (character == null || character.level != Level || character.rank != op.rank || character.exp != op.exp ||
                    character.talentLevel + ":" + character.talentTokenCount + ":" + character.equippedWeaponInstanceId != otherState)
                    throw new InvalidDataException("角色突破状态不一致。");
            }
            else
            {
                var weapon = save.inventory.equips.SingleOrDefault(w => w.instanceId == Participant.Target.WeaponInstanceId);
                if (weapon == null || weapon.level != Level || weapon.rank != op.rank || weapon.exp != op.exp || weapon.itemId + ":" + weapon.refinementLevel != otherState)
                    throw new InvalidDataException("武器突破状态不一致。");
            }
            if (op.materials.Length != Materials.Length || op.materials.Where((m, i) => m.ItemId != Materials[i].ItemId || m.Count != Materials[i].Count).Any())
                throw new InvalidDataException("突破材料回执不一致。");
            var counts = Materials.Select((m, i) =>
            {
                int count = save.inventory.stacks.SingleOrDefault(s => s.itemId == m.ItemId)?.count ?? 0;
                if (count != OldCounts[i] - m.Count || count < 0) throw new InvalidDataException("突破材料数量不一致。");
                return count;
            }).ToArray();
            Participant.Apply(op.rank, op.exp); ApplyGold(gold.amount);
            for (int i = 0; i < counts.Length; i++) GameContext.Instance.InventoryRepository.ApplyStackCount(Materials[i].ItemId, counts[i]);
            session.AcceptPromotion(Reply);
        }
    }
    internal async UniTask<CloudPromoteOperation> PromoteAsync(PromotionParticipant participant, Action<int, int> predicted, CancellationToken token = default)
    {
        var operation = pendingProgressionOperation as PromoteProgression;
        if (operation == null || !ReferenceEquals(operation.Participant.Model, participant.Model) || progressionSource != GameContext.Instance.ActiveSaveDirectory)
            operation = new PromoteProgression(participant, predicted, PromoteSending);
        await RunProgressionAsync(operation, token); return operation.Reply.operation;
    }
}
