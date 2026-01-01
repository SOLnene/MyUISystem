using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    public int id;
    //外部唯一标识
    public string key;
    public string itemName;
    public string desc;
    public string iconPath;
    public ItemCategory category;
    public ItemRarity itemRarity;
    public int stars;
}
