using System;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;

namespace Game.UI.Components.CharacterDetail
{
    public class CharacterTalentViewModel : IDisposable
    {
        public IReadOnlyReactiveProperty<int> TalentLevel => model.TalentLevelRP;
        public IReadOnlyReactiveProperty<int> TalentTokenCount => model.TalentTokenCountRP;
        public IReadOnlyReactiveProperty<bool> CanActivate => canActivate;
        public IReadOnlyReactiveProperty<bool> CanActivateSelected => canActivateSelected;
        public IReadOnlyReactiveProperty<int> SelectedNodeIndex => selectedNodeIndex;
        public IReadOnlyReactiveProperty<string> SelectedNodeText => selectedNodeText;
        public IReadOnlyReactiveProperty<string> SelectedPanelText => selectedPanelText;
        public IReadOnlyReactiveProperty<string> ActivateButtonText => activateButtonText;
        internal IReadOnlyReactiveProperty<string> SelectedTalentName => selectedTalentName;
        internal IReadOnlyReactiveProperty<string> SelectedTalentLayerText => selectedTalentLayerText;
        internal IReadOnlyReactiveProperty<string> SelectedTalentDescription => selectedTalentDescription;
        internal IReadOnlyReactiveProperty<bool> SelectedNodeActivated => selectedNodeActivated;
        internal int TalentCostItemId => talentSet != null ? talentSet.TalentCostItemId : 0;
        internal int TalentCostCount => talentSet != null ? talentSet.TalentCostCount : 1;
        public int NodeCount => talentSet != null ? talentSet.NodeCount : CharacterModel.MaxTalentLevel;

        readonly CharacterModel model;
        CharacterTalentSetDefinition talentSet;
        readonly ReactiveProperty<bool> canActivate = new ReactiveProperty<bool>();
        readonly ReactiveProperty<bool> canActivateSelected = new ReactiveProperty<bool>();
        readonly ReactiveProperty<int> selectedNodeIndex = new ReactiveProperty<int>(-1);
        readonly ReactiveProperty<string> selectedNodeText = new ReactiveProperty<string>(string.Empty);
        readonly ReactiveProperty<string> selectedPanelText = new ReactiveProperty<string>(string.Empty);
        readonly ReactiveProperty<string> activateButtonText = new ReactiveProperty<string>("激活");
        readonly ReactiveProperty<string> selectedTalentName = new ReactiveProperty<string>(string.Empty);
        readonly ReactiveProperty<string> selectedTalentLayerText = new ReactiveProperty<string>(string.Empty);
        readonly ReactiveProperty<string> selectedTalentDescription = new ReactiveProperty<string>(string.Empty);
        readonly ReactiveProperty<bool> selectedNodeActivated = new ReactiveProperty<bool>();
        readonly List<TalentEffectDefinition> activeEffects = new List<TalentEffectDefinition>();
        readonly CompositeDisposable disposable = new CompositeDisposable();

        public CharacterTalentViewModel(CharacterModel model)
        {
            this.model = model;
            Observable.CombineLatest(
                    model.TalentLevelRP,
                    model.TalentTokenCountRP,
                    (level, tokenCount) => level < CharacterModel.MaxTalentLevel && tokenCount > 0)
                .Subscribe(value => canActivate.Value = value)
                .AddTo(disposable);
            model.TalentLevelRP
                .Subscribe(_ =>
                {
                    RefreshSelectedNodeText();
                    RefreshTalentStats();
                })
                .AddTo(disposable);
            model.TalentTokenCountRP
                .Subscribe(_ => RefreshSelectedNodeText())
                .AddTo(disposable);
            model.Stats.BaseHP
                .Subscribe(_ => RefreshTalentStats())
                .AddTo(disposable);
            model.Stats.BaseAtk
                .Subscribe(_ => RefreshTalentStats())
                .AddTo(disposable);
            model.Stats.BaseDef
                .Subscribe(_ => RefreshTalentStats())
                .AddTo(disposable);
        }

        public void SetTalentSet(CharacterTalentSetDefinition definition)
        {
            talentSet = definition;
            RefreshSelectedNodeText();
            RefreshTalentStats();
        }

        public void SelectNode(int index)
        {
            if (index < 0 || index >= NodeCount)
            {
                return;
            }

            selectedNodeIndex.Value = index;
            RefreshSelectedNodeText();
        }

        public void ActivateTalent()
        {
            if (selectedNodeIndex.Value != model.TalentLevel)
            {
                SelectNode(model.TalentLevel);
            }

            ActivateSelectedTalent();
        }

        public void ActivateSelectedTalent()
        {
            if (!canActivateSelected.Value)
            {
                return;
            }

            if (model.TryActivateTalent())
            {
                SelectNode(model.TalentLevel - 1);
                RefreshTalentStats();
            }
        }

        public IReadOnlyList<TalentEffectDefinition> GetActiveEffects()
        {
            if (talentSet == null)
            {
                activeEffects.Clear();
                return activeEffects;
            }

            talentSet.CollectActiveEffects(model.TalentLevel, activeEffects);
            return activeEffects;
        }

        void RefreshSelectedNodeText()
        {
            var node = talentSet != null ? talentSet.GetNode(selectedNodeIndex.Value) : null;
            if (node == null)
            {
                canActivateSelected.Value = false;
                selectedNodeText.Value = string.Empty;
                selectedPanelText.Value = string.Empty;
                selectedTalentName.Value = string.Empty;
                selectedTalentLayerText.Value = string.Empty;
                selectedTalentDescription.Value = string.Empty;
                selectedNodeActivated.Value = false;
                activateButtonText.Value = "激活";
                return;
            }

            int layer = selectedNodeIndex.Value + 1;
            selectedNodeActivated.Value = selectedNodeIndex.Value < model.TalentLevel;
            selectedNodeText.Value = $"{node.Name}\n{node.Description}";
            selectedPanelText.Value = $"{node.Name}\n天赋 第{layer}层\n\n{node.Description}";
            selectedTalentName.Value = node.Name;
            selectedTalentLayerText.Value = $"天赋 第{layer}层";
            selectedTalentDescription.Value = node.Description;
            canActivateSelected.Value = selectedNodeIndex.Value == model.TalentLevel
                && model.TalentLevel < NodeCount
                && model.TalentTokenCount > 0;
            activateButtonText.Value = GetActivateButtonText();
        }

        string GetActivateButtonText()
        {
            if (selectedNodeIndex.Value < model.TalentLevel)
            {
                return "已激活";
            }

            if (selectedNodeIndex.Value > model.TalentLevel)
            {
                return "未解锁";
            }

            return model.TalentTokenCount > 0 ? "激活" : "信物不足";
        }

        void RefreshTalentStats()
        {
            float hp = 0f;
            float atk = 0f;
            float def = 0f;
            var effects = GetActiveEffects();

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                switch (effect.StatType)
                {
                    case TalentStatType.MaxHp:
                        hp += GetModifiedValue(model.Stats.BaseHP.Value, effect);
                        break;
                    case TalentStatType.Attack:
                        atk += GetModifiedValue(model.Stats.BaseAtk.Value, effect);
                        break;
                    case TalentStatType.Defense:
                        def += GetModifiedValue(model.Stats.BaseDef.Value, effect);
                        break;
                }
            }

            model.Stats.TalentBonusHP.Value = hp;
            model.Stats.TalentBonusAtk.Value = atk;
            model.Stats.TalentBonusDef.Value = def;
        }

        static float GetModifiedValue(float baseValue, TalentEffectDefinition effect)
        {
            return effect.ModifierType == TalentModifierType.Percent
                ? baseValue * effect.Value
                : effect.Value;
        }

        public void Dispose()
        {
            disposable.Dispose();
            canActivate.Dispose();
            canActivateSelected.Dispose();
            selectedNodeIndex.Dispose();
            selectedNodeText.Dispose();
            selectedPanelText.Dispose();
            activateButtonText.Dispose();
            selectedTalentName.Dispose();
            selectedTalentLayerText.Dispose();
            selectedTalentDescription.Dispose();
            selectedNodeActivated.Dispose();
        }
    }
}
