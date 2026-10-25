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
    [SerializeField] GachaCost cost = new GachaCost(223, 160);
    internal GachaCost Cost { get => cost; set => cost = value; }
    // Remote-only rules are intentionally excluded from serialized legacy assets.
    internal GachaEventRules EventRules { get; set; }
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
