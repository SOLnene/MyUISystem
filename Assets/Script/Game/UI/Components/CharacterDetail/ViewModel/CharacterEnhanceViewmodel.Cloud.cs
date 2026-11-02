using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using Game.Domain.Enhance;
using UniRx;

namespace Game.UI.Components.CharacterDetail
{
    public partial class CharacterEnhanceViewmodel
    {
        readonly ProgressionActionState enhancementState = new();
        internal ReactiveProperty<bool> enhancementBusy => enhancementState.Busy;
        internal readonly Subject<Unit> requestSyncEnhancement = new();
        internal readonly Subject<string> requestEnhanceTip = new();
        static bool IsCloudEnhancement => CloudGameSaveSession.UsesServerSettlement && CloudGameSaveSession.IsCloudSource(GameContext.Instance.ActiveSaveDirectory);
        internal bool HasPendingEnhancement => IsCloudEnhancement && (CloudGameSaveSession.Current?.HasPendingCharacterEnhance ?? false) &&
            CloudGameSaveSession.Current.PendingCharacterEnhance.characterKey == ((CharacterModel)model).Definition.key;
        bool IsEnhancementInputLocked => enhancementBusy.Value || (CloudGameSaveSession.Current?.HasPendingProgression ?? false);

        int AvailableBookCount(string key) => GameContext.Instance.InventoryRepository.GetItemCount(GameDatabase.ItemDatabase.GetItemByKey(key).id);

        void InitializeCloudEnhancement()
        {
            if (materialInput is ExpBookMaterialInput books) books.BindInventory(AvailableBookCount);
            GameContext.Instance.InventoryRepository.ObserveChanged().Subscribe(_ =>
            {
                if (materialInput is ExpBookMaterialInput input) input.RefreshSelectionRules();
            }).AddTo(disposable);
            enhancementState.BindSending(GameSaveCoordinator.Instance.CharacterEnhanceSending,
                () => requestSyncEnhancement.OnNext(Unit.Default));
            if (IsCloudEnhancement) LoadCharacterEnhanceRulesAsync().Forget();
            else enhancementState.Ready = true;
        }

        UniTask LoadCharacterEnhanceRulesAsync() => enhancementState.LoadRulesAsync(
            async (session, token) =>
            {
                if (!session.HasPendingProgression) await session.LoadCharacterEnhanceConfigAsync(token);
                return session.CharacterEnhanceConfig != null;
            },
            () =>
            {
                ((CharacterModel)model).NotifyEnhanceRulesChanged();
                if (materialInput is ExpBookMaterialInput input) input.RefreshSelectionRules();
            },
            error => requestEnhanceTip.OnNext("角色升级规则加载失败，请重试。" + error.Message));

        async UniTask SubmitCloudEnhancementAsync()
        {
            if (enhancementBusy.Value || enhancementState.IsDisposed) return;
            if (!enhancementState.Ready && !HasPendingEnhancement) { await LoadCharacterEnhanceRulesAsync(); return; }
            if (materialInput.GetTotalExp() <= 0 && !HasPendingEnhancement) return;
            var character = (CharacterModel)model;
            var books = materialInput.Counts.Where(pair => pair.Value.Value > 0)
                .Select(pair => new CharacterEnhanceBookSelection { ItemId = GameDatabase.ItemDatabase.GetItemByKey(pair.Key).id, Count = pair.Value.Value })
                .OrderBy(book => book.ItemId).ToArray();
            try
            {
                var receipt = await GameSaveCoordinator.Instance.EnhanceCharacterAsync(character, books, (beforeLevel, beforeExp) =>
                {
                    if (enhancementState.IsDisposed) return;
                    materialInput.Clear();
                    // The existing animation runs immediately; only authoritative confirmation may open the promotion page.
                    requestPlayEnhanceResult.OnNext(new EnhanceResultData(beforeLevel, character.LevelRP.Value,
                        (float)beforeExp / character.EnhanceExpRequired(beforeLevel), GetExpProgress(),
                        character.LevelRP.Value - beforeLevel, false, GetRarityColor()));
                });
                if (!enhancementState.IsDisposed)
                {
                    onUpgrade.Execute(Unit.Default);
                    EventBus<CharacterEnhanceCompletedEvent>.Raise(new CharacterEnhanceCompletedEvent(
                        new EnhanceResultData(receipt.oldLevel, character.LevelRP.Value,
                            (float)receipt.oldExp / character.EnhanceExpRequired(receipt.oldLevel), GetExpProgress(),
                            character.LevelRP.Value - receipt.oldLevel, character.LevelRP.Value >= character.GetCurrentMaxLevel(), GetRarityColor())));
                }
            }
            catch (Exception error)
            {
                await enhancementState.ReportFailureAsync(error, () => HasPendingEnhancement,
                    "养成结果需要同步，请重新登录。", "升级结果待确认，请点击升级重试；也可重新登录恢复。",
                    "升级未结算，已恢复本次预演。", "character_enhance_config_changed",
                    requestEnhanceTip.OnNext, LoadCharacterEnhanceRulesAsync);
            }
            finally
            {
                if (!enhancementState.IsDisposed) requestSyncEnhancement.OnNext(Unit.Default);
            }
        }

        void DisposeCloudEnhancement()
        {
            // Stop page-owned config work; an operation already journaled must continue independently of the page.
            enhancementState.Dispose();
            requestSyncEnhancement.Dispose(); requestEnhanceTip.Dispose();
        }
    }
}
