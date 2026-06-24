using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public static class CharacterFactory
{
    public static CharacterModel Create(CharacterDefinition definition, int level = 1,int exp = 0,int rank =0, int talentLevel = 0)
    {
        CharacterModel character =  new CharacterModel(
            definition,level,exp,rank,talentLevel
            );
        //给角色一把初始武器
        var defaultWeaponDef = GameDatabase.ItemDatabase.GetItemByKey("Claymore_Default") as EquipDefinition;
        if (defaultWeaponDef == null)
        {
            Debug.LogError("Default weapon not found: Claymore_Default");
            return character;
        }

        var defaultWeapon = new EquipItem(defaultWeaponDef);
        character.ChangeEquip(defaultWeapon);
        return character;
    }
}
