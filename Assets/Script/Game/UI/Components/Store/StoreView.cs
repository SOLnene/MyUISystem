using System;
using System.Threading;
using UniRx;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class StoreView : UIView
{
    [SerializeField]
    StoreTopView topView;
    [SerializeField]
    StoreTabView tabView;
    [SerializeField]
    StoreItemListView itemListView;
    [SerializeField]
    PurchasePopupView purchasePopup;
    [SerializeField]
    AnimatedPanel topPanel;
    [SerializeField]
    AnimatedPanel tabPanel;
    [SerializeField]
    AnimatedPanel itemAreaPanel;
    [SerializeField]
    GameObject inputBlock;

    StoreViewModel viewModel;
    readonly CompositeDisposable disposable = new();
    bool isClosing;
    bool panelsShown;
    bool isPurchasing;
    bool isRefreshing;
    CancellationTokenSource storeLifetime;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        isClosing = false;
        panelsShown = false;
        isPurchasing = false;
        isRefreshing = false;
        storeLifetime?.Cancel();
        storeLifetime?.Dispose();
        storeLifetime = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        disposable.Clear();
        topView.Bind(OnCancel);
        viewModel = new StoreViewModel(GameDatabase.StoreConfigDatabase, GameDatabase.ItemDatabase);
        tabView.Bind(viewModel);
        purchasePopup.HideImmediate();
        viewModel.CurrentTab
            .Subscribe(tab =>
            {
                topView.BindCurrencies(viewModel.GetVisibleCurrencyItemIds(tab));
            })
            .AddTo(disposable);
        viewModel.Items
            .Subscribe(items =>
            {
                itemListView.Bind(items, OnStoreItemClicked);
                if (panelsShown) itemListView.ShowItems().Forget();
            })
            .AddTo(disposable);

        ShowPanels().Forget();
        if (viewModel.UsesCloud) RefreshStoreLoop(storeLifetime.Token).Forget();
    }

    public override void OnClose()
    {
        storeLifetime?.Cancel();
        storeLifetime?.Dispose();
        storeLifetime = null;
        disposable.Clear();
        viewModel?.Dispose();
        base.OnClose();
    }

    public override void OnCancel()
    {
        if (isClosing || isPurchasing)
        {
            return;
        }

        isClosing = true;
        CloseWithAnimation().Forget();
    }

    async UniTask ShowPanels()
    {
        inputBlock.SetActive(true);
        await UniTask.WhenAll(topPanel.Show(), tabPanel.Show(), itemAreaPanel.Show());
        if (isClosing)
        {
            return;
        }

        await itemListView.ShowItems();
        if (isClosing)
        {
            return;
        }

        panelsShown = true;
        inputBlock.SetActive(false);
    }

    async UniTask CloseWithAnimation()
    {
        inputBlock.SetActive(true);
        await UniTask.WhenAll(topPanel.Hide(), tabPanel.Hide(), itemAreaPanel.Hide());
        base.OnCancel();
    }

    void OnStoreItemClicked(StoreItemViewModel itemViewModel)
    {
        if (isPurchasing || isRefreshing) return;
        if (!viewModel.TryCreatePurchasePopupData(itemViewModel.StoreItemId, out PurchasePopupViewData popupData))
        {
            return;
        }

        purchasePopup.Bind(popupData, OnPurchaseConfirmed, null, OnPurchaseItemAreaClicked);
        purchasePopup.Show();
    }

    void OnPurchaseItemAreaClicked(ItemDefinition itemDefinition)
    {
        if (itemDefinition == null)
        {
            return;
        }

        UIManager.Instance.OpenWithView(UIType.InfoPanelPopup, itemDefinition);
    }

    void OnPurchaseConfirmed(PurchasePopupViewData popupData, int count)
    {
        if (isPurchasing || isRefreshing) return;
        PurchaseItemAsync(popupData, count, storeLifetime.Token).Forget();
    }

    async UniTask PurchaseItemAsync(PurchasePopupViewData popupData, int count, CancellationToken cancellationToken)
    {
        isPurchasing = true;
        purchasePopup.HideImmediate();
        inputBlock.SetActive(true);
        topView.ShowStoreStatus("正在确认购买…");
        try
        {
            await viewModel.PurchaseAsync(popupData.StoreItemId, count, cancellationToken);
            if (viewModel.UsesCloud) await viewModel.RefreshCloudAsync(cancellationToken);
            topView.ShowStoreStatus(null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                topView.ShowStoreStatus("等待刷新");
                UIManager.Instance.Open(UIType.TipView, StoreFeedback.Message(error));
                if (viewModel.UsesCloud && !(CloudGameSaveSession.Current?.HasPendingStore ?? false))
                    await RefreshStoreAsync(cancellationToken);
            }
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                isPurchasing = false;
                inputBlock.SetActive(false);
            }
        }
    }

    async UniTask RefreshStoreLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!isPurchasing) await RefreshStoreAsync(cancellationToken);
                // Use elapsed time against the server's deadline; changing the local clock cannot reset stock.
                double delay = viewModel.RefreshDelay;
                await UniTask.Delay(TimeSpan.FromSeconds(delay > 0 ? Math.Min(30, delay + 0.1) : 5),
                    ignoreTimeScale: true, cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    async UniTask RefreshStoreAsync(CancellationToken cancellationToken)
    {
        isRefreshing = true;
        topView.ShowStoreStatus("正在刷新…");
        try
        {
            bool changed = await viewModel.RefreshCloudAsync(cancellationToken);
            if (changed) purchasePopup.HideImmediate();
            topView.ShowStoreStatus(null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                topView.ShowStoreStatus("暂时不可用，稍后重试");
                Debug.LogWarning(StoreFeedback.Message(error));
            }
        }
        finally { if (!cancellationToken.IsCancellationRequested) isRefreshing = false; }
    }
}
