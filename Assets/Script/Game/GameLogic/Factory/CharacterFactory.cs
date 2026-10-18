using System;
using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public static class CharacterFactory
{
    public static CharacterModel Create(CharacterDefinition definition, int level = 1,int exp = 0,int rank =0, int talentLevel = 0, int talentTokenCount = 0)
    {
        CharacterModel character =  new CharacterModel(
            definition,level,exp,rank,talentLevel,talentTokenCount
            );
        //给角色一把初始武器
        var defaultWeapon = new EquipItem(GetStarterWeaponDefinition(definition.weaponType));
        character.ChangeEquip(defaultWeapon);
        return character;
    }

    internal static EquipDefinition GetStarterWeaponDefinition(string weaponType)
    {
        // Fixed keys keep starter grants independent of item-table ordering.
        string key = weaponType switch
        {
            "Sword" => "Sword_Dawn",
            "Claymore" => "Claymore_Reasoning",
            "Polearm" => "Pole_Noire",
            "Bow" => "Bow_Sling",
            "Catalyst" => "Catalyst_Pulpfic",
            _ => throw new InvalidOperationException($"Unknown starter weapon type: {weaponType}")
        };
        var weapon = GameDatabase.ItemDatabase.GetItemByKey(key) as EquipDefinition;
        if (weapon == null || weapon.weaponType != weaponType || weapon.stars != 3)
            throw new InvalidOperationException($"Invalid three-star starter weapon: {key}");
        return weapon;
    }
}
