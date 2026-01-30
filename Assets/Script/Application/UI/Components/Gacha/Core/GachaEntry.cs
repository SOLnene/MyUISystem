using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GachaEntry
{
    public string entryKey;   // itemKey / characterKey
    public GachaEntryType entryType;
    public int weight;
    public int rarity;
}
