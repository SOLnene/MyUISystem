using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GameEconomy : SingletonMono<GameEconomy>
{
    const int PrimogemItemId = 201; // 原石
    const int GoldItemId = 202; // 金币
    const int GenesisCrystalItemId = 203; // 创世结晶
    const int StarglitterItemId = 221; // 无主的星辉
    const int StardustItemId = 222; // 无主的星尘
    const int IntertwinedFateItemId = 223; // 纠缠之缘
    const int AcquaintFateItemId = 224; // 相遇之缘

    [SerializeField]
    List<CurrencyInitialValue> initialCurrencies = new()
    {
        new CurrencyInitialValue(PrimogemItemId, 4682),
        new CurrencyInitialValue(GoldItemId, 100000),
        new CurrencyInitialValue(GenesisCrystalItemId, 0),
        new CurrencyInitialValue(StarglitterItemId, 7),
        new CurrencyInitialValue(StardustItemId, 60),
        new CurrencyInitialValue(IntertwinedFateItemId, 10),
        new CurrencyInitialValue(AcquaintFateItemId, 10),
    };

    readonly Dictionary<int, ReactiveProperty<int>> currencies = new();
    readonly Subject<int> currencyChanged = new();

    public ReactiveProperty<int> gold => GetCurrencyProperty(GoldItemId);
    public IObservable<int> CurrencyChanged => currencyChanged;

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
        currencyChanged.OnNext(itemId);
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
        currencyChanged.OnNext(itemId);
    }

    public CurrencySaveData ExportSaveData()
    {
        CurrencySaveData saveData = new CurrencySaveData();
        foreach (KeyValuePair<int, ReactiveProperty<int>> pair in currencies)
        {
            saveData.items.Add(new CurrencyAmountSaveData(pair.Key, pair.Value.Value));
        }

        return saveData;
    }

    public void ImportSaveData(CurrencySaveData saveData)
    {
        InitializeCurrencies();
        if (saveData == null || saveData.items == null)
        {
            return;
        }

        foreach (CurrencyAmountSaveData item in saveData.items)
        {
            currencies[item.itemId] = new ReactiveProperty<int>(Mathf.Max(0, item.amount));
            currencyChanged.OnNext(item.itemId);
        }
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
