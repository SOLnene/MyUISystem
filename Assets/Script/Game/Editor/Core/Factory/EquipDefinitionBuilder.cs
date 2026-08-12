using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EquipDefinitionBuilder
{
    public static void FillEquip(EquipDefinition equip, string typeToken, ItemRarity rarity)
    {
        equip.baseAttack = UnityEngine.Random.Range(30, 150);
        equip.baseCritRate = UnityEngine.Random.Range(0.05f, 0.2f);
        equip.baseCritDamage = UnityEngine.Random.Range(0.5f, 1.0f);
        equip.rankInfos = RankInfoGenerator.Generate(
            (int)rarity + 1,
            1000,
            $"mat_{typeToken.ToLower()}_"
            );
    }
}
