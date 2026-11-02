using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using Game.Domain.Enhance;
using UniRx;
using UnityEngine;
using UnityEngine.TextCore.Text;
namespace Game.UI.Components.CharacterDetail
//todo:做成通用
{
    public partial class CharacterEnhanceViewmodel : IDisposable
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
        readonly ReadOnlyReactiveProperty<EnhancementPreviewData> enhancementPreview;
        //目前这两个接口没什么用，现在还是角色专用vm
        public CharacterEnhanceViewmodel(IEnhanceable model, IMaterialInput materialInput, Action onBack)
        {
            this.model = model;
            promoteModel = model as IPromotable;
            //todo:避免强转
            modelName = (model as CharacterModel).Name.Value;
            this.onBack = onBack;
            
            this.materialInput = materialInput;
            enhancementPreview = EnhancementPreviewCalculator.Observe(model, materialInput.TotalExpRp, materialInput.GetTotalGold)
                .ToReadOnlyReactiveProperty().AddTo(disposable);
            previewVm = new EnhanceLevelPreviewViewModel(enhancementPreview);

            var statItemList = new List<StatItemViewModel>();
            var previews = enhancementPreview.Value.Stats;
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

            enhancementPreview.Subscribe(
                value =>
                {
                    UpdatePreview(value);
                }).AddTo(disposable);

            InitializeCloudEnhancement();
        }

        public void UpdatePreview(int exp)
        {
            UpdatePreview(EnhancementPreviewCalculator.ForTarget(model, exp, materialInput.GetTotalGold()));
        }

        void UpdatePreview(EnhancementPreviewData data)
        {
            var previews = data.Stats;

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
            if (IsEnhancementInputLocked) return;
            if (type != ExpBookType.None)
                materialInput.Add(ChangeToKey(type), count);
        }

        public void RemoveBook(ExpBookType type, int count = 1)
        {
            if (IsEnhancementInputLocked) return;
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
            if (IsCloudEnhancement) { SubmitCloudEnhancementAsync().Forget(); return; }
            if (IsEnhancementInputLocked) return;
            int exp = materialInput.GetTotalExp();
            if (exp <= 0)
            {
                return;
            }

            int oldLevel = model.LevelRP.Value;
            float oldProgress = GetExpProgress();
            int levelUpCount = previewVm.levelUpCount.Value;
            if (!ClientProgressionService.TryEnhanceCharacter((CharacterModel)model, materialInput)) return;
            int newLevel = model.LevelRP.Value;
            float newProgress = GetExpProgress();
            bool needSwitchContent = newLevel >= model.GetCurrentMaxLevel();
            Color rarityColor = GetRarityColor();
            materialInput.Clear();
            var result = new EnhanceResultData(oldLevel, newLevel, oldProgress, newProgress, levelUpCount, needSwitchContent, rarityColor);
            requestPlayEnhanceResult.OnNext(result);
            AchievementProgressService.Instance.AddProgress(
                AchievementProgressKeys.CharacterEnhance);
            onUpgrade.Execute(Unit.Default);
            GameSaveCoordinator.Instance.MarkDirty();
            // 业务事件在强化结果实际产生后发送，教程、成就或埋点都可独立观察。
            EventBus<CharacterEnhanceCompletedEvent>.Raise(
                new CharacterEnhanceCompletedEvent(result));
        }

        float GetExpProgress()
        {
            int max = ((CharacterModel)model).EnhanceExpRequired(model.LevelRP.Value);
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
            if (IsEnhancementInputLocked) return;
            int currentLevel = model.LevelRP.Value;
            int currentExp = model.ExpRP.Value;
            int maxLevel = model.GetCurrentMaxLevel();

            int needExp = 0;

            // 计算到当前阶级满级所需经验
            for (int lv = currentLevel; lv < maxLevel; lv++)
            {
                int levelExp = ((CharacterModel)model).EnhanceExpRequired(lv);

                if (lv == currentLevel)
                    needExp += levelExp - currentExp;
                else
                    needExp += levelExp;
            }

            materialInput.Clear();

            var keys = ExpBookRules.Keys;

            int remainExp = needExp;

            // 先用大的经验书
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                int bookExp = ExpBookRules.Experience(keys[i]);
                int count = Math.Min(remainExp / bookExp, AvailableBookCount(keys[i]));

                if (count >= 1)
                {
                    materialInput.Add(keys[i], count);
                    remainExp -= count * bookExp;
                }
            }
            if (remainExp > 0)
            {
                foreach (var key in keys)
                    if (materialInput.Counts[key].Value < AvailableBookCount(key))
                    { materialInput.Add(key, 1); break; }
            }

            int totalExp = materialInput.GetTotalExp();
            if (totalExp > 0)
            {
                // 快速添加确实选入材料后才发送成功事件，避免按钮点击造成误判。
                EventBus<CharacterQuickFillCompletedEvent>.Raise(
                    new CharacterQuickFillCompletedEvent(totalExp));
            }
        }

        public void SetOnBack(Action command)
        {
            onBack = command;
        }
        
        public void Dispose()
        {
            DisposeCloudEnhancement();
            disposable.Dispose();
            previewVm.Dispose();
            foreach (var itemSlotVm in itemViewModels)
            {
                itemSlotVm.Dispose();
            }
        }
    }
}
