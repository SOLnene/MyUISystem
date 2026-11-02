using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using UniRx;
using UnityEngine;
using UnityEngine.Android;

namespace Game.UI.Components.CharacterDetail
{
    /// <summary>
    /// 角色突破面板 VM：
    /// 1. 显示当前阶级（星星）与等级 / 下一阶等级上限
    /// 2. 显示基础生命值 / 防御力 / 攻击力 / 暴击伤害在当前阶段与下一阶段满级时的预览
    /// </summary>
    public class CharacterPromoteViewmodel : IDisposable
    {
        public readonly IPromotable model;
        internal readonly PromotionActionViewModel promotion;

        // 阶级与等级显示
        public ReadOnlyReactiveProperty<int> rank;
        public ReadOnlyReactiveProperty<string> currentLevelText;
        public ReadOnlyReactiveProperty<string> nextLevelCapText;
        public ReadOnlyReactiveProperty<string> goldCostText;
        
        public List<StatItemViewModel> statItemViewModels;

        public List<ItemSlotViewModel> itemSlotViewModels;
        
        public ReactiveCommand onPromote = new ReactiveCommand();
        public readonly Subject<PromoteLevelResultData> requestPlayPromoteResult = new();
        public Action onBack;
        // 当前阶段（当前 Rank 的等级上限）与下一阶段（下一 Rank 的等级上限）下的基础属性预览
        public 
        
        readonly CompositeDisposable disposable = new CompositeDisposable();

        public CharacterPromoteViewmodel(IPromotable model, Action onBack)
        {
            this.model = model;
            this.onBack = onBack;

            rank = model.RankRP.ToReadOnlyReactiveProperty().AddTo(disposable);
            
            currentLevelText = model.RankRP.Select(_ =>
                {
                    var currentCap = GetCurrentLevel();
                    return $"等级{currentCap}/{currentCap}";
                })
                .ToReadOnlyReactiveProperty().AddTo(disposable);

            nextLevelCapText = model.RankRP.Select(_ =>
            {
                var nextCap = PromotionParticipant.For((IEnhanceable)model).NextLevelCap;
                return $"等级上限开放至{nextCap}级";
            }).ToReadOnlyReactiveProperty().AddTo(disposable);

            goldCostText = model.RankRP.Select(_ =>
            {
                var cost = model.GetPromoteGoldCost();
                return $"{cost}";
            }).ToReadOnlyReactiveProperty().AddTo(disposable);
            
            var previews = model.GetStatPreview(0);
            statItemViewModels = new List<StatItemViewModel>();
            foreach (var preview in previews)
            {
                var statItem = new StatItemViewModel(
                    icon: null, label: preview.label);
                statItem.SetValue(preview.currentValue, preview.nextValue);
                statItemViewModels.Add(statItem);
            }
            model.ChangeRP.Subscribe(_ =>
            {
                UpdatePreview(0,true);
            }).AddTo(disposable);

            itemSlotViewModels = new List<ItemSlotViewModel>();
            //现在只有一个rule，先写死
            CreateMaterialVMs(rank.Value);
            promotion = new PromotionActionViewModel(PromotionParticipant.For((IEnhanceable)model));
            promotion.PlayResult.Subscribe(requestPlayPromoteResult.OnNext).AddTo(disposable);
        }

        public void UpdatePreview(int exp,bool promoting)
        {
            // Keep this operation's before/after attributes while its predicted rank is awaiting confirmation.
            if (promotion?.Pending == true) return;
            var previews = model.GetStatPreview(exp,promoting);

            for (int i = 0; i < previews.Count; i++)
            {
                statItemViewModels[i].SetValue(previews[i].currentValue, previews[i].nextValue);
                Debug.Log($"{previews[i].label} current: {previews[i].currentValue} next: {previews[i].nextValue}");
            }
        }
        
        void CreateMaterialVMs(int currentRank)
        {
            foreach (var slot in itemSlotViewModels) slot.Dispose();
            itemSlotViewModels.Clear();
            itemSlotViewModels.AddRange(PromotionActionViewModel.CreateMaterialSlots((IEnhanceable)model));
        }

        public void RefreshMaterialVMs()
        {
            CreateMaterialVMs(model.RankRP.Value);
        }
        
        public bool Promote()
        {
            if (PromotionParticipant.IsCloud) { promotion.SubmitAsync().Forget(); return false; }
            if (GameSaveCoordinator.Instance.PromoteSending.Value) return false;
            int oldRank = model.RankRP.Value;
            int oldMaxLevel = model.GetCurrentMaxLevel();
            int currentLevel = GetCurrentLevel();
            if (ClientProgressionService.TryPromote(PromotionParticipant.For((IEnhanceable)model)))
            {
                int newRank = model.RankRP.Value;
                int newMaxLevel = model.GetCurrentMaxLevel();
                Color rarityColor = GetRarityColor();
                requestPlayPromoteResult.OnNext(new PromoteLevelResultData(oldRank, newRank, currentLevel, oldMaxLevel, newMaxLevel, rarityColor));
                onPromote.Execute(Unit.Default);
                GameSaveCoordinator.Instance.MarkDirty();
                return true;
            }
            return false;
        }

        int GetCurrentLevel()
        {
            var characterModel = model as CharacterModel;
            return characterModel != null ? characterModel.LevelRP.Value : model.GetCurrentMaxLevel();
        }

        Color GetRarityColor()
        {
            var characterModel = model as CharacterModel;
            if (characterModel == null)
                return Color.white;

            int rarity = Mathf.Clamp(characterModel.Definition.rarity - 1, 0, RarityConfig.Colors.Length - 1);
            return RarityConfig.GetColor(rarity);
        }
        
        public void Dispose()
        {
            promotion.Dispose();
            disposable.Dispose();
        }
    }
}
