using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGachaPoolProvider
{
    GachaDefinition GetPool(GachaPoolType type);
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

    public GachaDefinition GetPool(GachaPoolType type)
    {
        string key = schedule.GetActiveGachaKey(type);
        if (key == null)
        {
            Debug.LogError( $"No active gacha key for pool type {type}");
            return null;
        }
        return database.GetPool(key);
    }
}
