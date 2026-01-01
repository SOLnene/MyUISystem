using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
    Max = 5
}


/// <summary>
/// 物品分类（用于区分显示逻辑）
/// </summary>
public enum ItemCategory
{
    Equip,
    Consumable,
    Material,
    QuestItem,
    All
}



[Serializable]
public class CategoryConfig
{
    public ItemCategory category;
    public string displayName; // UI显示用
    public Sprite icon;        // 图标可选
}


/// <summary>
/// 运行时实例
/// </summary>
[Serializable]
public class InventoryItem
{
    public ItemDefinition ItemDefinition{ get; private set; }
    
    public int Id => ItemDefinition.id;
    public string ItemName => ItemDefinition.itemName;
    public string Key => ItemDefinition.key;
    public string Desc => ItemDefinition.desc;
    public string IconPath => ItemDefinition.iconPath;
    public ItemCategory Category => ItemDefinition.category;
    
    public ItemRarity ItemRarity => ItemDefinition.itemRarity;

    public int Stars => ItemDefinition.stars;
    
    public InventoryItem(ItemDefinition item)
    {
        ItemDefinition = item;
    }
    
    public virtual string GetDisplayName() => ItemName;
    
    public virtual string GetDisplayMainText() => "";
    
    public virtual string GetDisplaySubText() => "";
    
    public virtual string GetDisplayLevelText() => "";
}


[Serializable]
public class ConsumableItem : InventoryItem
{
    public int Count { get; private set; }

    public ConsumableItem(ItemDefinition def, int count = 1) : base(def)
    {
        Count = count;
    }

    public void Add(int amount) => Count += amount;
    public void Use(int amount) => Count = Mathf.Max(0, Count - amount);

    public override string GetDisplayLevelText() => $"×{Count}";
}

/// <summary>
/// 稀有度对应颜色
/// </summary>
public static class RarityConfig
{
    public static readonly Color[] Colors = new Color[]
    {
        new Color(0.55f, 0.55f, 0.55f),   // Common - 灰色（略暖）
        new Color(0.38f, 0.63f, 0.35f),   // Uncommon - 柔和草绿色
        new Color(0.36f, 0.52f, 0.78f),   // Rare - 暗蓝色（类似星辉背景）
        new Color(0.58f, 0.46f, 0.78f),   // Epic - 柔紫色（接近原神四星）
        new Color(0.85f, 0.65f, 0.35f),   // Legendary - 暖橙金色（接近五星武器背景）
    };
    public static Color GetColor(ItemRarity itemRarity)
    {
        int rarity = (int)itemRarity;
        if (rarity >= 0 && rarity < Colors.Length)
            return Colors[rarity];
        return Color.white;
    }
    
    
    public static Color GetColor(int rarity)
    {
        if (rarity >= 0 && rarity < Colors.Length)
            return Colors[rarity];
        return Color.white;
    }
}


