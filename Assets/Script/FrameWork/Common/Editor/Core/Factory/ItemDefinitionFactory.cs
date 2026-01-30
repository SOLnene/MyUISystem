using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ItemDefinitionFactory
{
    public static ItemDefinition Create(
        ItemSpriteParseResult result)
    {
        Type defType = ResolveDefinitionType(result);
        var so = ScriptableObject.CreateInstance(defType) as ItemDefinition;
        if (so == null)
        {
            return null;
        }
        FillBase(so, result);
        //todo:拿出去
        if (so is EquipDefinition equip)
        {
            EquipDefinitionBuilder.FillEquip(equip,result.typeToken,so.itemRarity);
        }
        return so;
    }

    static Type ResolveDefinitionType(ItemSpriteParseResult result)
    {
        string lower = result.typeToken.ToLower();

        if (lower is "sword" or "bow" or "claymore" or "polearm" or "catalyst" or "weapon" or "equip")
            return typeof(EquipDefinition);

        return typeof(ItemDefinition);
    }
    
    static ItemCategory ParseCategory(string typeToken)
    {
        string lower = typeToken.ToLower();
        if (lower is "sword" or "bow" or "claymore" or "weapon" or "equip")
            return ItemCategory.Equip;
        if (lower.Contains("consumable"))
            return ItemCategory.Consumable;
        if (lower.Contains("material"))
            return ItemCategory.Material;
        return ItemCategory.All;
    }

    static void FillBase(ItemDefinition so, ItemSpriteParseResult result)
    {
        so.id = Math.Abs(result.assetName.GetHashCode());
        so.key = result.assetName;
        so.itemName = result.displayName.Replace('_', ' ');
        so.iconPath = result.addressKey;
        so.category = result.category;
        so.itemRarity = (ItemRarity)Random.Range(0, 5);
        so.desc = "自动生成的物品";
    }
    
    
}

