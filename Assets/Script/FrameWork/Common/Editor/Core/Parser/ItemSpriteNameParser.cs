using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ItemSpriteNameParser
{
    public static bool TryParse(string fileName,
        out ItemSpriteParseResult result)
    {
        result = default;
        
        var parts = fileName.Split('_');
        if (parts.Length < 3)
        {
            return false;
        }

        string typeToken = FindTypeToken(parts);
        if (string.IsNullOrEmpty(typeToken))
        {
            return false;
        }
        
        int typeIndex = Array.FindIndex(parts, p =>
            string.Equals(p, typeToken, StringComparison.OrdinalIgnoreCase));

        string name = typeIndex >= 0 && typeIndex < parts.Length - 1
            ? string.Join("_", parts.Skip(typeIndex + 1))
            : parts.Last();

        result = new ItemSpriteParseResult
        {
            fileName = fileName,
            assetName = $"{typeToken}_{name}",
            displayName = name,
            typeToken = typeToken,
            category = ParseItemCategory(typeToken),
            addressKey = fileName.ToLower()
        };
        return true;
    }

    /// <summary>
    /// 找到类型标记
    /// </summary>
    /// <param name="parts"></param>
    /// <returns></returns>
    public static string FindTypeToken(string[] parts)
    {
        string[] keywords =
        {
            "sword","claymore","bow","polearm","catalyst",
            "weapon","equip","consumable","material","quest"
        };

        foreach (var p in parts)
        {
            if (keywords.Contains(p.ToLower()))
            {
                return p;
            }
        }
        return null;
    }
    
    public static ItemCategory ParseItemCategory(string typeToken)
    {
        if (string.IsNullOrEmpty(typeToken)) return ItemCategory.All;
        string lower = typeToken.ToLower();
        if (lower == "sword" || lower == "claymore" || lower == "bow" || lower == "polearm" || lower == "catalyst" || lower == "weapon" || lower == "equip")
            return ItemCategory.Equip;
        if (lower.Contains("potion") || lower.Contains("consumable") || lower.Contains("food"))
            return ItemCategory.Consumable;
        if (lower.Contains("material") || lower.Contains("ore"))
            return ItemCategory.Material;
        return ItemCategory.All;
    }
}

//目前用class方便拓展
public class ItemSpriteParseResult
{
    public string fileName;         // 原始文件名 调试 & 回溯用
    public string assetName;        // Sword_Darker 唯一稳定标识（Editor 层）
    public string displayName;      // Darker 给玩家看的
    public string typeToken;        // Sword 语义来源
    public ItemCategory category;   //派生但需要缓存的结果
    public string addressKey;       //资源定位规则
}