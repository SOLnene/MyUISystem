using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class CharacterTabItemViewModel
{
    int index;
    public TabType tabType;
    public Action onClick;
    public string label;

    public ReactiveProperty<bool> selected = new ReactiveProperty<bool>();
    public CharacterTabItemViewModel(TabType type,Action onClick)
    {
        this.tabType = type;
        this.onClick = onClick;
        
    }


}

/// <summary>
/// 角色详情页Tab类型
/// </summary>
public enum TabType
{
    Attribute,
    Equip,
    Relic,
    Detail
}