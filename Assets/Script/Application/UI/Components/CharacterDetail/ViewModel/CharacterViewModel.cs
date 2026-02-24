using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

/// <summary>
/// 用于展示角色详情界面（CharacterDetailView）的数据和状态
/// </summary>
public class CharacterViewModel
{
    public ReactiveProperty<string> Name { get; } = new ReactiveProperty<string>();
    public ReactiveProperty<int> Level { get; } = new();
    public ReactiveProperty<int> Star { get; }= new();
    public ReactiveProperty<int> Attack { get; }= new();
    public ReactiveProperty<int> Defend { get;  }= new();
    public ReactiveProperty<int> ElementalMastery { get; }= new();
    public ReactiveProperty<int> CapacityLimit { get; }= new();
    public ReactiveProperty<string> Description { get;  }= new();
    
}
