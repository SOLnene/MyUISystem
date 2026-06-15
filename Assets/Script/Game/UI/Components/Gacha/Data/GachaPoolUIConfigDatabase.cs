using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Database/Gacha Pool UI Config Database")]
public class GachaPoolUIConfigDatabase : ScriptableObject
{
    public List<GachaPoolUIConfig> configs;

    public IReadOnlyList<GachaPoolUIConfig> Configs => configs;

    public GachaPoolUIConfig Get(GachaPoolType type)
        => configs.Find(c => c.poolType == type);
}
