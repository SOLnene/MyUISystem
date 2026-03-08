using System;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;
namespace Game.UI.Components.CharacterDetail
{
    /// <summary>
    /// 目前是信息界面的vm
    /// </summary>
    public class AttributePageViewModel : IDisposable
    {
        public List<StatItemViewModel> stats = new List<StatItemViewModel>();
        CompositeDisposable disposable = new CompositeDisposable();
        public AttributePageViewModel(CharacterModel model)
        {
            AddStat("生命值上限", model.Stats.FinalHP);
            AddStat("攻击力", model.Stats.FinalAtk);
            AddStat("防御力", model.Stats.FinalDef);
            AddStat("元素精通", model.Stats.ElementalMastery);
            AddStat("体力上限", model.Stats.Stamina);
            AddStat("好感度", model.Stats.Favor);
        }

        void AddStat(string label, IReadOnlyReactiveProperty<float> source)
        {
            var vm = new StatItemViewModel(null, label);
            source.Subscribe(v => vm.SetValue(v, v))
                .AddTo(disposable);
            Debug.Log($"Added stat: {label}{source.Value}");
            stats.Add(vm);
        }
        
        public void Dispose()
        {
            disposable.Dispose();
        }
    }
}
