using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public interface IMaterialInput
{
    public Dictionary<string, ReactiveProperty<int>> Counts { get; }
    IReadOnlyReactiveProperty<int> TotalExpRp { get; }
    IReadOnlyReactiveProperty<int> TotalGoldRp { get; }

    int GetTotalExp();
    int GetTotalGold();

    void Add(string key, int amount = 1);

    void Remove(string key, int amount = 1);
    public void Clear();
}
