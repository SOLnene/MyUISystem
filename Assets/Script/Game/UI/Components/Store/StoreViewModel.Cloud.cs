using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public partial class StoreViewModel
{
    bool remoteInitialized;
    internal bool UsesCloud => purchaseService.UsesCloud;
    internal double RefreshDelay => purchaseService.RefreshDelay;

    internal async UniTask<bool> RefreshCloudAsync(CancellationToken cancellationToken)
    {
        string previousToken = purchaseService.ConfigurationToken, previousPeriod = purchaseService.CurrentPeriod;
        var database = await purchaseService.LoadCloudStoreAsync(cancellationToken);
        bool changed = !remoteInitialized || previousToken != purchaseService.ConfigurationToken || previousPeriod != purchaseService.CurrentPeriod;
        if (changed)
        {
            currencyDisposables.Clear();
            foreach (var item in allItemViewModels) item.Dispose();
            allItemViewModels.Clear(); itemViewModelsById.Clear();
            itemViewModelsByCategory.Clear(); itemViewModelsByCostItemId.Clear();
            storeDatabase = database;
            InitializeItemViewModels(); ObserveCostCurrencies(); RefreshVisibleItems();
            remoteInitialized = true;
        }
        else foreach (var item in allItemViewModels) item.RefreshPurchasePreview();
        return changed;
    }

    internal async UniTask PurchaseAsync(int id, int count, CancellationToken cancellationToken)
    {
        if (UsesCloud && CloudGameSaveSession.UsesServerSettlement)
        {
            await purchaseService.PurchaseCloudAsync(id, count, cancellationToken);
            foreach (var item in allItemViewModels) item.RefreshPurchasePreview();
        }
        else if (!TryPurchase(id, count)) throw new InvalidOperationException("购买失败，请检查余额和剩余购买次数。");
    }
}
