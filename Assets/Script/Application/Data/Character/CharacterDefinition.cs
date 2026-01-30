using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    public int id;
    public string key;  //来自image名称
    public string displayName;

    public int baseHP;
    public int baseAttack;
    public int baseDefense;

    public int rarity;

    public string element;
    public string weaponType;
}
