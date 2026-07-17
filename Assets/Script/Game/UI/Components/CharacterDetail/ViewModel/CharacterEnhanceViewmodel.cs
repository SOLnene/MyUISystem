using System;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;
using UnityEngine.TextCore.Text;
namespace Game.UI.Components.CharacterDetail
//todo:做成通用
{
    public class CharacterEnhanceViewmodel : IDisposable
    {
        public IEnhanceable model;
        public IPromotable promoteModel;
        public EnhanceLevelPreviewViewModel previewVm;
        public IMaterialInput materialInput;

        public List<StatItemViewModel> statItemViewModels;

        /// <summary>
        /// itemSlots的vm
        /// </summary>
        public List<ItemSlotViewModel> itemViewModels = new List<ItemSlotViewModel>();

        //专用 todo:可以不用rp
        public ReactiveProperty<ExpBookType> selectedBook = new ReactiveProperty<ExpBookType>(ExpBookType.None);

        public ReactiveCommand onUpgrade = new ReactiveCommand();
        public readonly Subject<EnhanceResultData> requestPlayEnhanceResult = new();

        public Action onBack;
        //名字
        public string modelName;
        
        CompositeDisposable disposable = new CompositeDisposable();
        //目前这两个接口没什么用，现在还是角色专用vm
        public CharacterEnhanceViewmodel(IEnhanceable model, IMaterialInput materialInput, Action onBack)
        {
            this.model = model;
            promoteModel = model as IPromotable;
            //todo:避免强转
            modelName = (model as CharacterModel).Name.Value;
            this.onBack = onBack;
            
            this.materialInput = materialInput;
            previewVm = new EnhanceLevelPreviewViewModel(model, materialInput.TotalExpRp);

            var statItemList = new List<StatItemViewModel>();
            var previews = model.GetStatPreview(0);
            foreach (var preview in previews)
            {
                var statItem = new StatItemViewModel(
                    icon: null, label: preview.label);
                statItem.SetValue(preview.currentValue, preview.nextValue);
                statItemList.Add(statItem);
            }
            statItemViewModels = statItemList;

            //材料部分
            CreateItemViewmodel("expbook_small");
            CreateItemViewmodel("expbook_medium");
            CreateItemViewmodel("expbook_large");

            materialInput.TotalExpRp.Subscribe(
                value =>
                {
                    UpdatePreview(value);
                }).AddTo(disposable);

        }

        public void UpdatePreview(int exp)
        {
            var previews = model.GetStatPreview(exp);

            for (int i = 0; i < previews.Count; i++)
            {
                statItemViewModels[i].SetValue(previews[i].currentValue, previews[i].nextValue);
                Debug.Log($"{previews[i].label} current: {previews[i].currentValue} next: {previews[i].nextValue}");
            }
        }

        //与materialinput做转接
        public string ChangeToKey(ExpBookType type)
        {
            switch (type)
            {
                case ExpBookType.Small:
                    return "expbook_small";
                case ExpBookType.Medium:
                    return "expbook_medium";
                case ExpBookType.Large:
                    return "expbook_large";
                default:
                    Debug.Log("invalid book type");
                    break;
            }
            throw new ArgumentException($"Invalid book type: {type}");
        }

        public ExpBookType ChangeToBook(string key)
        {
            switch (key)
            {
                case "expbook_small":
                    return ExpBookType.Small;
                case "expbook_medium":
                    return ExpBookType.Medium;
                case "expbook_large":
                    return ExpBookType.Large;
                default:
                    Debug.Log("invalid book definition");
                    break;
            }
            return ExpBookType.None;
        }

        public void AddBook(ExpBookType type, int count = 1)
        {
            if (type != ExpBookType.None)
                materialInput.Add(ChangeToKey(type), count);
        }

        public void RemoveBook(ExpBookType type, int count = 1)
        {
            if (type != ExpBookType.None)
                materialInput.Remove(ChangeToKey(type), count);
        }

        public int GetCurrentBookCount()
        {
            if (selectedBook.Value == ExpBookType.None) return 0;
            var key = ChangeToKey(selectedBook.Value);
            return materialInput.Counts[key].Value;
        }

        public void ConfirmEnhance()
        {
            int exp = materialInput.GetTotalExp();
            if (exp <= 0)
            {
                return;
            }

            int oldLevel = model.LevelRP.Value;
            float oldProgress = GetExpProgress();
            int levelUpCount = previewVm.levelUpCount.Value;
            model.AddExp(exp);
            int newLevel = model.LevelRP.Value;
            float newProgress = GetExpProgress();
            bool needSwitchContent = newLevel >= model.GetCurrentMaxLevel();
            Color rarityColor = GetRarityColor();
            materialInput.Clear();
            requestPlayEnhanceResult.OnNext(new EnhanceResultData(oldLevel, newLevel, oldProgress, newProgress, levelUpCount, needSwitchContent, rarityColor));
            onUpgrade.Execute(Unit.Default);
            GameSaveCoordinator.Instance.MarkDirty();
        }

        float GetExpProgress()
        {
            int max = model.LevelSystem.GetExpRequired(model.LevelSystem.Level);
            if (max <= 0)
                return 0f;

            return (float)model.LevelSystem.CurrentExp / max;
        }

        Color GetRarityColor()
        {
            var characterModel = model as CharacterModel;
            if (characterModel == null)
                return Color.white;

            int rarity = Mathf.Clamp(characterModel.Definition.rarity - 1, 0, RarityConfig.Colors.Length - 1);
            return RarityConfig.GetColor(rarity);
        }

        public void CreateItemViewmodel(string key)
        {
            var item = new InventoryItem(GameDatabase.ItemDatabase.GetItemByKey(key));
            var viewModel = new ItemSlotViewModel(item);
            viewModel.onClick.Subscribe(
                _ =>
                {
                    SetSelectedBook(ChangeToBook(key));
                }).AddTo(disposable);
            selectedBook.Subscribe(
                value =>
                {
                    viewModel.isSelected.Value = value == ChangeToBook(key);
                }).AddTo(disposable);
            materialInput.Counts[key]
                .Subscribe(count =>
                {
                    viewModel.selectedCount.Value = count;
                })
                .AddTo(disposable);

            itemViewModels.Add(viewModel);

        }

        public void SetSelectedBook(ExpBookType type)
        {
            selectedBook.Value = type;
        }

        /// <summary>
        /// 快捷加入：自动选择经验书以让角色升级到当前阶级的满级，优先消耗低级书，减少经验溢出
        /// </summary>
        public void QuickFill()
        {
            int currentLevel = model.LevelRP.Value;
            int currentExp = model.ExpRP.Value;
            int maxLevel = model.GetCurrentMaxLevel();

            int needExp = 0;

            // 计算到当前阶级满级所需经验
            for (int lv = currentLevel; lv < maxLevel; lv++)
            {
                int levelExp = model.LevelSystem.GetExpRequired(lv);

                if (lv == currentLevel)
                    needExp += levelExp - currentExp;
                else
                    needExp += levelExp;
            }

            materialInput.Clear();

            int[] expValue = { 100, 500, 2500 };
            string[] keys = { "expbook_small", "expbook_medium", "expbook_large" };

            int remainExp = needExp;

            // 先用大的经验书
            for (int i = expValue.Length - 1; i >= 0; i--)
            {
                int count = remainExp / expValue[i];

                if (count >= 1)
                {
                    materialInput.Add(keys[i], count);
                    remainExp -= count * expValue[i];
                }
            }
            if (remainExp > 0)
            {
                materialInput.Add("expbook_small", 1);
            }
        }

        public void SetOnBack(Action command)
        {
            onBack = command;
        }
        
        public void Dispose()
        {
            disposable.Dispose();
            previewVm.Dispose();
            foreach (var itemSlotVm in itemViewModels)
            {
                itemSlotVm.Dispose();
            }
        }
    }
}
