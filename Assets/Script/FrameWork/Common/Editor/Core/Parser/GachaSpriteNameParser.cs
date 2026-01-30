using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// gahca图片名称解析器
/// </summary>
public static class GachaSpriteNameParser
{

    
    public static bool TryParse(
        string fileName,
        out string characterKey,
        out CharacterVisualType type)
    {
        characterKey = null;
        type = default;

        fileName = fileName.ToLowerInvariant();
        
        if (fileName.StartsWith("ui_gacha_avataricon_"))
        {
            characterKey = fileName.Replace("ui_gacha_avataricon_", "");
            type = CharacterVisualType.Icon;
            return true;
        }

        if (fileName.StartsWith("ui_gacha_avatarimg_"))
        {
            characterKey = fileName.Replace("ui_gacha_avatarimg_", "");
            type = CharacterVisualType.Image;
            return true;
        }

        return false;
    }
    
    // 决定存放子文件夹名
    private static string ParseCategoryFolder(string typeToken)
    {
        var cat = ItemSpriteNameParser.ParseItemCategory(typeToken);
        switch (cat)
        {
            case ItemCategory.Equip: return "Equip";
            case ItemCategory.Consumable: return "Consumable";
            case ItemCategory.Material: return "Material";
            case ItemCategory.QuestItem: return "QuestItem";
            default: return "Misc";
        }
    }
    
}

//语义信息
public enum CharacterVisualType
{
    Icon,
    Image
}