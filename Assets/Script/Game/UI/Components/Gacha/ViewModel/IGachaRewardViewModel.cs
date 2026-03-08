using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 
/// </summary>
public interface IGachaRewardViewModel
{
    string Name { get; }
    int Star { get; }
    GachaRewardType Type { get; }
}

/// <summary>
/// 
/// </summary>
public enum GachaRewardType
{
    Character,
    Weapon,
    Item
}