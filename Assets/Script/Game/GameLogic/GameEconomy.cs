using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GameEconomy : SingletonMono<GameEconomy>
{
    const int GoldItemId = 201; // 金币
    const int GenesisCrystalItemId = 202; // 创世结晶
    const int PrimogemItemId = 203; // 原石
    const int IntertwinedFateItemId = 221; // 纠缠之缘
    const int AcquaintFateItemId = 222; // 相遇之缘
    const int StarglitterItemId = 223; // 无主的星辉
    const int StardustItemId = 224; // 无主的星尘

    [SerializeField]
    List<CurrencyInitialValue> initialCurrencies = new()
    {
        new CurrencyInitialValue(GoldItemId, 100000),
        new CurrencyInitialValue(GenesisCrystalItemId, 0),
        new CurrencyInitialValue(PrimogemItemId, 4682),
        new CurrencyInitialValue(IntertwinedFateItemId, 10),
        new CurrencyInitialValue(AcquaintFateItemId, 10),
        new CurrencyInitialValue(StarglitterItemId, 7),
        new CurrencyInitialValue(StardustItemId, 60),
    };

    readonly Dictionary<int, ReactiveProperty<int>> currencies = new();

    public ReactiveProperty<int> gold => GetCurrencyProperty(GoldItemId);

    protected override void Awake()
    {
        base.Awake();
        InitializeCurrencies();
    }

    public int GetCurrency(int itemId)
    {
        return GetCurrencyProperty(itemId).Value;
    }

    public IReadOnlyReactiveProperty<int> ObserveCurrency(int itemId)
    {
        return GetCurrencyProperty(itemId);
    }

    public bool TrySpendGold(int amount)
    {
        return TrySpendCurrency(GoldItemId, amount);
    }

    public bool TrySpendCurrency(int itemId, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        ReactiveProperty<int> currency = GetCurrencyProperty(itemId);
        if (currency.Value < amount)
        {
            Debug.LogWarning($"货币不足: {itemId}");
            return false;
        }

        currency.Value -= amount;
        return true;
    }

    public void AddGold(int amount)
    {
        AddCurrency(GoldItemId, amount);
    }

    public void AddCurrency(int itemId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        ReactiveProperty<int> currency = GetCurrencyProperty(itemId);
        currency.Value += amount;
    }

    void InitializeCurrencies()
    {
        currencies.Clear();

        foreach (CurrencyInitialValue initialValue in initialCurrencies)
        {
            currencies[initialValue.ItemId] = new ReactiveProperty<int>(Mathf.Max(0, initialValue.Amount));
        }
    }

    ReactiveProperty<int> GetCurrencyProperty(int itemId)
    {
        if (!currencies.TryGetValue(itemId, out ReactiveProperty<int> currency))
        {
            Debug.LogError($"未注册货币: {itemId}");
            currency = new ReactiveProperty<int>(0);
            currencies[itemId] = currency;
        }

        return currency;
    }

    [System.Serializable]
    public class CurrencyInitialValue
    {
        [SerializeField]
        int itemId;
        [SerializeField]
        int amount;

        public int ItemId => itemId;
        public int Amount => amount;

        public CurrencyInitialValue(int itemId, int amount)
        {
            this.itemId = itemId;
            this.amount = amount;
        }
    }
}
