using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class UITabSelector : MonoBehaviour
{
    [SerializeField]
    List<UITabToggle> tabToggles;

    [SerializeField]
    ToggleGroup toggleGroup;
    
    public readonly ReactiveProperty<int> selectedIndex = new ReactiveProperty<int>(0);

    public event Action<int> OnTabChanged;

    /*void Awake()
    {
        foreach (var toggle in tabToggles)
            toggle.SetToggleGroup(toggleGroup);
    }*/

    public void Bind(IReactiveProperty<int> externalSelectedIndex)
    {
        for (int i = 0; i < tabToggles.Count; i++)
        {
            int index = i;
            tabToggles[i].Init(toggleGroup);
            tabToggles[i].isOn.Where(isOn => isOn).Subscribe(_ =>
            {
                externalSelectedIndex.Value = index;
            }).AddTo(this);

        }
        externalSelectedIndex.Subscribe(index =>
        {   
            OnTabSelected(index);
        }).AddTo(this);
    }
    

    void OnTabSelected(int activeIndex)
    {
        for(int i=0;i<tabToggles.Count;i++)
        {
            tabToggles[i].SetIsOn(i == activeIndex);
        }
    }
    
    /// <summary>
    /// 外部调用
    /// </summary>
    /// <param name="index"></param>
    public void Select(int index)
    {
        if (index > 0 || index <= tabToggles.Count)
        {
            selectedIndex.Value = index;
            OnTabSelected(index);
            OnTabChanged?.Invoke(index);
        }
    }
}
