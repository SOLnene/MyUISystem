using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public enum ExpBookType{
    Small,
    Medium,
    Large,
    None,
}

public class ExpBookMaterialInput : IMaterialInput
{
    public  Dictionary<string, ReactiveProperty<int>> Counts { get; }

    public IReadOnlyReactiveProperty<int> TotalExpRp { get; }
    public IReadOnlyReactiveProperty<int> TotalGoldRp { get; }

    public ExpBookMaterialInput()
    {
        Counts = new Dictionary<string, ReactiveProperty<int>>()
        {
            { "expbook_small", new ReactiveProperty<int>(0) },
            { "expbook_medium", new ReactiveProperty<int>(0) },
            { "expbook_large", new ReactiveProperty<int>(0) },
        };
        TotalExpRp = Observable.Merge(Counts.Values)
            .Select(_ => GetTotalExp())
            .ToReadOnlyReactiveProperty();
        TotalGoldRp = Observable.Merge(Counts.Values)
            .Select(_ => GetTotalGold())
            .ToReadOnlyReactiveProperty();
    }
    
    public int GetBookExp(string key)
    {
        switch(key)
                {
                    case "expbook_small": return 100;
                    case "expbook_medium": return 500;
                    case "expbook_large": return 2500;
                    default: return 0;
                }
    }
    
    public int GetBookCost(string key)
    {
        return GrowthCostFormula.GetEnhanceGoldCost(GetBookExp(key));
    }

    public int GetTotalExp()
    {
        int result = 0;
        foreach (var kv in Counts)
        {
            result += kv.Value.Value * GetBookExp(kv.Key);
        }
        return result;
    }

    public int GetTotalGold()
    {
        int result = 0;
        foreach (var kv in Counts)
        {
            result += kv.Value.Value * GetBookCost(kv.Key);
        }
        return result;
    }
    
    public void Add(string key, int amount = 1)
    {
        Counts[key].Value += amount;
    }

    public void Remove(string key, int amount = 1)
    {
        // todo:确保不会减到负数
        Counts[key].Value = Mathf.Max(0, Counts[key].Value - amount);
    }
    
    // 增加：清除所有选择（关闭界面或强化成功后使用）
    public void Clear()
    {
        foreach (var rp in Counts.Values) rp.Value = 0;
    }
}   
