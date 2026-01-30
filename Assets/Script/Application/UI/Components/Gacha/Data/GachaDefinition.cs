using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Gacha Definition")]
public class GachaDefinition : ScriptableObject
{
    public string gachaKey;

    public List<GachaEntry> entries = new List<GachaEntry>();
    
    //保底规则  
    public int pityCount = 90;
    public int upPityCount = 180;
}


public enum GachaEntryType
{
    Character,
    Equip
}

public enum GachaPoolType
{
    Equip,
    Character,
    Mixed
}
