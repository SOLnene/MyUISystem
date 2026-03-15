using UniRx;
using UnityEngine;
namespace Game.UI.Components.CharacterDetail
{
    public class StatItemViewModel
    {
        //todo:存标识，做enum
        //无subsribe时，可以不手动dispose
        public Sprite icon;
    
        public string label;

        public readonly ReactiveProperty<float> currentValue = new ReactiveProperty<float>();
        public readonly ReactiveProperty<float> nextValue = new ReactiveProperty<float>(); 
        
        public IReadOnlyReactiveProperty<string> valueText;
        public IReadOnlyReactiveProperty<string> nextValueText;
        
        public IReadOnlyReactiveProperty<bool> IsUpgrade { get; }
        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="icon"></param>
        /// <param name="label"></param>
        /// <param name="currentValue"></param>
        /// <param name="nextValue"></param> 无升级时，传当前值
        public StatItemViewModel(Sprite icon, string label)
        {
            this.icon = icon;
            this.label = label;
            valueText = currentValue
                .Select(v => v.ToString("N0"))
                .ToReadOnlyReactiveProperty();
            nextValueText = nextValue
                .Select(v => v.ToString("N0"))
                .ToReadOnlyReactiveProperty();
            IsUpgrade = currentValue
                .CombineLatest(nextValue, (c, n) => n > c)
                .ToReadOnlyReactiveProperty();
        }
        
        public void SetValue(float current,float next)
        {
            currentValue.Value = current;
            nextValue.Value = next;
        }
    }
}
