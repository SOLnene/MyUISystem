using UniRx;
using UnityEngine;
namespace Game.UI.Components.CharacterDetail
{
    public enum StatValueFormat
    {
        Number,
        Percent
    }
    
    public class StatItemViewModel
    {
        //todo:存标识，做enum
        //无subsribe时，可以不手动dispose
        public Sprite icon;
    
        public string label;
        readonly StatValueFormat valueFormat;

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
        public StatItemViewModel(Sprite icon, string label, StatValueFormat valueFormat = StatValueFormat.Number)
        {
            this.icon = icon;
            this.label = label;
            this.valueFormat = valueFormat;
            valueText = currentValue
                .Select(FormatValue)
                .ToReadOnlyReactiveProperty();
            nextValueText = nextValue
                .Select(FormatValue)
                .ToReadOnlyReactiveProperty();
            IsUpgrade = currentValue
                .CombineLatest(nextValue, (c, n) => n > c)
                .ToReadOnlyReactiveProperty();
        }

        string FormatValue(float value)
        {
            return valueFormat == StatValueFormat.Percent
                ? value.ToString("P0")
                : value.ToString("N0");
        }
        
        public void SetValue(float current,float next)
        {
            currentValue.Value = current;
            nextValue.Value = next;
        }
    }
}
