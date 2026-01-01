using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用toggle组件
/// </summary>
public class UITabToggle : MonoBehaviour
{
    [SerializeField]
    Toggle toggle;
    [SerializeField]
    TextMeshProUGUI label;
    [SerializeField]
    RectTransform highlight;
    [SerializeField]
    UIAnimator animator;
    
    
    public readonly ReactiveProperty<bool> isOn = new ReactiveProperty<bool>(false);
    
    //public event Action<UITabToggle,bool> OnValueChanged;

    public void Init(ToggleGroup group)
    {
        toggle.group = group;
        //SetLabel(text);
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(OnValueChanged);
        UpdateVisual(isOn.Value);
    }
    
    void OnValueChanged(bool isOn)
    {
        this.isOn.Value = isOn;
        UpdateVisual(isOn);
    }
    
    public void SetLabel(string text)
    {
        if(label!=null)
            label.text = text;
    }
    
    public void SetIsOn(bool isOn,bool notify = true)
    {
        toggle.SetIsOnWithoutNotify(isOn);
        if (notify)
        {
            OnValueChanged(isOn);
        }
        
    }
    
    void UpdateVisual(bool active)
    {
        if (label != null)
        {
            label.fontSize = active ? 72 : 36;
        }
    }
}
