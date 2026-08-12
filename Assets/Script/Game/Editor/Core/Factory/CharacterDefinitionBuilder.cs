using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public class CharacterDefinitionBuilder
{
    public void Build(CharacterDefinition def)
    {
        // 基础默认值
        def.displayName = NameFromKey(def.key);
        def.baseHp = 100;
        def.baseAttack = 20;
        def.baseDefense = 10;
        def.baseElementalMastery = 0;
        def.rarity = ResolveRarity(def.key);
        def.element = "None";
        def.weaponType = "Sword";
        def.description = $"{def.displayName}的 description";
    }

    string NameFromKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "Unknown";

        return key.Replace("_", " ");
    }

    int ResolveRarity(string key)
    {
        if (key.Contains("SSR")) return 5;
        if (key.Contains("SR")) return 4;
        return 3;
    }
}
