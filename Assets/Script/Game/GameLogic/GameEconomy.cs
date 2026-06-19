using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GameEconomy : SingletonMono<GameEconomy>
{
    const int GoldItemId = 201;

    [SerializeField]
    List<CurrencyInitialValue> initialCurrencies = new()
    {
        new CurrencyInitialValue(GoldItemId, 100000),
        new CurrencyInitialValue(202, 0),
        new CurrencyInitialValue(203, 0),
        new CurrencyInitialValue(221, 10),
        new CurrencyInitialValue(222, 10),
    };

    public readonly ReactiveProperty<int> gold = new(100000);
    readonly Dictionary<int, ReactiveProperty<int>> currencies = new();

    protected override void Awake()
    {
        base.Awake();
        EnsureCurrencies();
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

    void EnsureCurrencies()
    {
        if (currencies.Count > 0)
        {
            return;
        }

        currencies[GoldItemId] = gold;
        foreach (CurrencyInitialValue initialValue in initialCurrencies)
        {
            if (initialValue.ItemId == GoldItemId)
            {
                gold.Value = Mathf.Max(0, initialValue.Amount);
                continue;
            }

            currencies[initialValue.ItemId] = new ReactiveProperty<int>(Mathf.Max(0, initialValue.Amount));
        }
    }

    ReactiveProperty<int> GetCurrencyProperty(int itemId)
    {
        EnsureCurrencies();
        if (!currencies.TryGetValue(itemId, out ReactiveProperty<int> currency))
        {
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
