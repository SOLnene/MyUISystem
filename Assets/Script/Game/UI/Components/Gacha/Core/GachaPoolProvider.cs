using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGachaPoolProvider
{
    GachaDefinition GetPool(string gachaKey);
}
//负责“拿到这个池的定义”
public class GachaPoolProvider : IGachaPoolProvider
{
    readonly GachaPoolDatabase database;

    public GachaPoolProvider(GachaPoolDatabase database)
    {
        this.database = database;
    }

    public GachaDefinition GetPool(string gachaKey)
    {
        if (string.IsNullOrEmpty(gachaKey))
        {
            Debug.LogError("Gacha key is empty");
            return null;
        }
        return database.GetPool(gachaKey);
    }
}
