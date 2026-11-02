using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using Game.Domain.Enhance;
using UniRx;
using UnityEngine;

public sealed partial class GameSaveCoordinator
{
    internal readonly ReactiveProperty<bool> CharacterEnhanceSending = new(false);

    sealed class CharacterEnhanceProgression : ProgressionOperation
    {
        internal readonly CharacterModel Character;
        internal readonly CharacterEnhanceBookSelection[] Books;
        internal int OldLevel, OldExp, OldGold, OldRank, OldTalent, OldTokens, Cost;
        internal long OldWeaponId;
        internal int[] OldBookCounts;
        internal CharacterEnhancePrediction Result;
        Action<int, int> predicted;
        internal CloudCharacterEnhanceReply reply;

        internal CharacterEnhanceProgression(CharacterModel character, CharacterEnhanceBookSelection[] books,
            Action<int, int> predicted, ReactiveProperty<bool> busy)
            : base(ProgressionKind.CharacterEnhance, character.Definition.key, busy,
                "请恢复原角色的升级结果，或重新登录同步。", "升级待确认期间本地进度发生变化，请重新登录同步。")
        {
            Character = character; this.predicted = predicted;
            Books = books?.Select(b => new CharacterEnhanceBookSelection { ItemId = b.ItemId, Count = b.Count }).ToArray();
        }

        internal override async UniTask PrepareAsync(CloudGameSaveSession session, CancellationToken token)
        {
            if (session.CharacterEnhanceConfig == null) await session.LoadCharacterEnhanceConfigAsync(token);
            if (CloudGameSaveSession.RequireSession(GameContext.Instance.ActiveSaveDirectory) != session)
                throw new InvalidOperationException("角色升级会话已经改变。");
            if (!ReferenceEquals(GameContext.Instance.CharacterRepository.GetByKey(TargetKey), Character))
                throw new InvalidOperationException("当前账号没有这个角色。");
            var rules = session.CharacterEnhanceConfig.rules;
            var totals = rules.BookTotals(Books);
            OldBookCounts = Books.Select(b => GameContext.Instance.InventoryRepository.GetItemCount(b.ItemId)).ToArray();
            if (Books.Where((b, i) => b.Count > OldBookCounts[i]).Any()) throw new InvalidOperationException("经验书数量不足。");
            OldLevel = Character.LevelRP.Value; OldExp = Character.ExpRP.Value; OldRank = Character.RankRP.Value;
            OldTalent = Character.TalentLevel; OldTokens = Character.TalentTokenCount;
            OldWeaponId = Character.CurrentEquipRP.Value?.InstanceId ?? 0;
            Result = rules.Preview(TargetKey, OldLevel, OldExp, OldRank, totals.Experience);
            if (Result.GainedExp <= 0) throw new InvalidOperationException("角色已达到当前等级上限。");
            Cost = totals.GoldCost; OldGold = GameEconomy.Instance.GetCurrency(rules.GoldItemId);
            if (OldGold < Cost) throw new InvalidOperationException("金币不足，无法升级。");
            session.BeginCharacterEnhancement(TargetKey, Books);
        }

        internal override void ApplyPrediction()
        { Character.ApplyEnhancementState(Result.Level, Result.Exp); ApplyGold(OldGold - Cost); }

        internal override void NotifyPrediction()
        {
            var notify = predicted; predicted = null;
            notify?.Invoke(OldLevel, OldExp);
        }
        internal override async UniTask ResolveAsync(CloudGameSaveSession session, CancellationToken token)
        { reply = await session.ResolveCharacterEnhancementAsync(token); }
        internal override bool MatchesLocal() => Applied && Character.LevelRP.Value == Result.Level && Character.ExpRP.Value == Result.Exp &&
            Character.RankRP.Value == OldRank && Character.TalentLevel == OldTalent && Character.TalentTokenCount == OldTokens &&
            (Character.CurrentEquipRP.Value?.InstanceId ?? 0) == OldWeaponId && GameEconomy.Instance.GetCurrency(202) == OldGold - Cost &&
            Books.Where((b, i) => GameContext.Instance.InventoryRepository.GetItemCount(b.ItemId) != OldBookCounts[i]).Any() == false;
        internal override void Restore()
        { Character.ApplyEnhancementState(OldLevel, OldExp); ApplyGold(OldGold); }
        internal override void Confirm(CloudGameSaveSession session)
        { ApplyCharacterEnhanceReply(reply, this); session.AcceptCharacterEnhancement(reply); }
    }

    internal async UniTask<CloudCharacterEnhanceOperation> EnhanceCharacterAsync(CharacterModel character, CharacterEnhanceBookSelection[] books,
        Action<int, int> predicted, CancellationToken token = default)
    {
        // Retrying confirms the original operation, including its original before-state for completion observers.
        var operation = pendingProgressionOperation as CharacterEnhanceProgression;
        if (operation == null || !ReferenceEquals(operation.Character, character) || progressionSource != GameContext.Instance.ActiveSaveDirectory)
            operation = new CharacterEnhanceProgression(character, books, predicted, CharacterEnhanceSending);
        await RunProgressionAsync(operation, token);
        return operation.reply.operation;
    }

    static void ApplyCharacterEnhanceReply(CloudCharacterEnhanceReply reply, CharacterEnhanceProgression prediction)
    {
        if (reply.state.revision != reply.operation.committedRevision)
            throw new InvalidDataException("升级后云端已有新进度，请重新登录读取完整存档。");
        var save = JsonUtility.FromJson<GameSaveData>(reply.state.saveJson);
        var op = reply.operation;
        var target = save?.characters?.characters?.SingleOrDefault(c => c.characterKey == prediction.TargetKey);
        var gold = save?.currencies?.items?.SingleOrDefault(c => c.itemId == 202);
        var progress = save?.achievements?.progress?.SingleOrDefault(p => p.progressKey == AchievementProgressKeys.CharacterEnhance);
        if (save?.version != 8 || target == null || save.inventory?.stacks == null || target.rank != prediction.OldRank ||
            target.talentLevel != prediction.OldTalent || target.talentTokenCount != prediction.OldTokens ||
            target.equippedWeaponInstanceId != prediction.OldWeaponId || op.oldLevel != prediction.OldLevel || op.oldExp != prediction.OldExp ||
            target.level != op.level || target.exp != op.exp || gold == null || gold.amount < 0 || gold.amount != prediction.OldGold - op.goldCost ||
            progress == null || progress.value < 1) throw new InvalidDataException("角色升级回执与存档内容不一致，请重新登录恢复。");
        var rules = CloudGameSaveSession.Current.CharacterEnhanceConfig.rules;
        rules.ValidateState(target.characterKey, target.level, target.exp, target.rank);
        int[] remaining = prediction.Books.Select((b, i) =>
        {
            int count = save.inventory.stacks.SingleOrDefault(s => s.itemId == b.ItemId)?.count ?? 0;
            if (count < 0 || count != prediction.OldBookCounts[i] - b.Count)
                throw new InvalidDataException("经验书回执与背包数量不一致，请重新登录恢复。");
            return count;
        }).ToArray();
        // Accept valid authoritative differences without replaying the upgrade animation or reporting a failure.
        prediction.Character.ApplyEnhancementState(target.level, target.exp); ApplyGold(gold.amount);
        for (int i = 0; i < remaining.Length; i++)
            GameContext.Instance.InventoryRepository.ApplyStackCount(prediction.Books[i].ItemId, remaining[i]);
        var achievements = AchievementProgressService.Instance;
        achievements.AddProgress(AchievementProgressKeys.CharacterEnhance,
            Math.Max(0, progress.value - achievements.GetProgress(AchievementProgressKeys.CharacterEnhance)));
    }
}
