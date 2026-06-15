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
    readonly IGachaSchedule schedule;

    public GachaPoolProvider(GachaPoolDatabase database,IGachaSchedule schedule)
    {
        this.database = database;
        this.schedule = schedule;
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
