using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GachaResult
{
    public List<GachaEntry> Entries { get; }
    internal IReadOnlyList<long> AwardedInstanceIds { get; }
    internal bool ServerConfirmed => AwardedInstanceIds != null;

    internal GachaResult(List<GachaEntry> entries, List<long> awardedInstanceIds)
        : this(entries)
    {
        AwardedInstanceIds = awardedInstanceIds;
    }

    public GachaResult(List<GachaEntry> entries)
    {
        Entries = entries;
    }
}
