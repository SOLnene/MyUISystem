using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGachaService
{
    GachaResult Draw(int count,GachaPoolType type);
}

public class GachaService: IGachaService
{
    private readonly IGachaPoolProvider poolProvider;

    //每个卡池保底计数
    Dictionary<string, int> pityCounters = new();

    public GachaService(IGachaPoolProvider provider)
    {
        poolProvider = provider;

    }
    
    public GachaResult Draw(int count,GachaPoolType type)
    {
        var pool = poolProvider.GetPool(type);
        if (pool == null || pool.entries.Count == 0)
        {
            Debug.LogWarning($"No pool found for type {type}");
            return new GachaResult(new List<GachaEntry>());
        }
        
        var entries = new List<GachaEntry>();

        // 获取这个池的保底计数
        if (!pityCounters.TryGetValue(pool.gachaKey, out int pityCounter))
        {
            pityCounter = 0;
        }
        
        for (int i = 0; i < count; i++)
        {
            var entry = DrawSingle(pool,ref pityCounter);
            entries.Add(entry);
        }
        pityCounters[pool.gachaKey] = pityCounter;

        return new GachaResult(entries);
    }

    GachaEntry DrawSingle(GachaDefinition pool,ref int pityCounter)
    {
        pityCounter++;
        if (pityCounter >= pool.pityCount)
        {
            pityCounter = 0;
            return GetHightestRarityEntry(pool);
        }
        return WeightedRandom(pool);
    }

    /// <summary>
    /// 权重抽卡
    /// </summary>
    /// <returns></returns>
    GachaEntry WeightedRandom(GachaDefinition pool)
    {
        int totalWeigh = 0;
        foreach (var e in pool.entries)
        {
            totalWeigh += e.weight;
        }

        int rand = Random.Range(0, totalWeigh);

        int acc = 0;
        foreach (var e in pool.entries)
        {
            acc += e.weight;
            if (rand < acc)
            {
                return e;
            }
        }
        return pool.entries[0];
    }
    
    /// <summary>
    /// 保底机制
    /// </summary>
    /// <returns></returns>
    GachaEntry GetHightestRarityEntry(GachaDefinition pool)
    {
        GachaEntry highest = null;
        foreach (var entry in pool.entries)
        {
            if (highest == null || entry.rarity > highest.rarity)
            {
                highest = entry;
            }
        }

        return highest;
    }
}
