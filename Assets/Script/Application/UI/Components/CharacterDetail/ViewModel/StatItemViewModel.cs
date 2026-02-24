using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class StatItemViewModel
{
    public Sprite icon;
    public string label;
    public IReadOnlyReactiveProperty<string> valueText;
    
    
    public StatItemViewModel(Sprite icon,string label, IReadOnlyReactiveProperty<float> value)
    {
        this.icon = icon;
        this.label = label;
        valueText = value
            .Select(v => v.ToString("NO"))
            .ToReadOnlyReactiveProperty();
    }
}
