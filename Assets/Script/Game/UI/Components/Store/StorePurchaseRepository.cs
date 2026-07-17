using System;
using System.Collections.Generic;
using UniRx;

class StorePurchaseRepository
{
    readonly Dictionary<int, StorePurchaseRecord> records = new();
    readonly Subject<int> changed = new();

    public IObservable<int> Changed => changed;

    public int GetRemainingCount(int storeItemId, int limitCount)
    {
        if (limitCount <= 0)
        {
            return 0;
        }

        StorePurchaseRecord record = GetTodayRecord(storeItemId);
        return Math.Max(0, limitCount - record.PurchasedCount);
    }

    public void AddPurchasedCount(int storeItemId, int count)
    {
        if (count <= 0)
        {
            return;
        }

        StorePurchaseRecord record = GetTodayRecord(storeItemId);
        record.PurchasedCount += count;
        changed.OnNext(storeItemId);
    }

    public StorePurchaseSaveData ExportSaveData()
    {
        StorePurchaseSaveData saveData = new StorePurchaseSaveData();
        foreach (KeyValuePair<int, StorePurchaseRecord> pair in records)
        {
            saveData.records.Add(new StorePurchaseRecordSaveData(
                pair.Key,
                pair.Value.PeriodKey,
                pair.Value.PurchasedCount));
        }

        return saveData;
    }

    public void ImportSaveData(StorePurchaseSaveData saveData)
    {
        records.Clear();
        if (saveData == null || saveData.records == null)
        {
            return;
        }

        foreach (StorePurchaseRecordSaveData recordData in saveData.records)
        {
            if (recordData == null || recordData.storeItemId <= 0 || string.IsNullOrEmpty(recordData.periodKey))
            {
                continue;
            }

            records[recordData.storeItemId] = new StorePurchaseRecord(
                recordData.periodKey,
                Math.Max(0, recordData.purchasedCount));
        }
    }

    StorePurchaseRecord GetTodayRecord(int storeItemId)
    {
        string periodKey = DateTime.Now.ToString("yyyy-MM-dd");
        if (!records.TryGetValue(storeItemId, out StorePurchaseRecord record))
        {
            record = new StorePurchaseRecord(periodKey);
            records.Add(storeItemId, record);
            return record;
        }

        if (record.PeriodKey != periodKey)
        {
            record.PeriodKey = periodKey;
            record.PurchasedCount = 0;
        }

        return record;
    }
}

class StorePurchaseRecord
{
    public string PeriodKey;
    public int PurchasedCount;

    public StorePurchaseRecord(string periodKey, int purchasedCount = 0)
    {
        PeriodKey = periodKey;
        PurchasedCount = purchasedCount;
    }
}
