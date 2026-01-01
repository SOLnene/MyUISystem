using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GameEconomy : SingletonMono<GameEconomy>
{
    public readonly ReactiveProperty<int> gold = new(100000);

    public bool TrySpendGold(int amount)
    {
        if (gold.Value < amount)
        {
            Debug.LogWarning("金币不足");
            return false;
        }
        gold.Value -= amount;
        return true;
    }

    public void AddGold(int amount)
    {
        gold.Value += amount;
    }
}