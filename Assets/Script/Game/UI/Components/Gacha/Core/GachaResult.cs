using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GachaResult
{
    public List<GachaEntry> Entries { get; }

    public GachaResult(List<GachaEntry> entries)
    {
        Entries = entries;
    }
}
