using UniRx;
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

    StoreViewModel viewModel;
    readonly CompositeDisposable disposable = new();

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        disposable.Clear();
        topView.Bind(OnCancel);
        viewModel = new StoreViewModel(GameContext.Instance.StoreDatabase, GameDatabase.ItemDatabase);
        tabView.Bind(viewModel);
        purchasePopup.Hide();
        viewModel.CurrentTab
            .Subscribe(tab => itemListView.Bind(viewModel.CreateItems(tab), OnStoreItemClicked))
            .AddTo(disposable);
    }

    public override void OnClose()
    {
        disposable.Clear();
        base.OnClose();
    }

    void OnStoreItemClicked(StoreItemViewData itemData)
    {
        if (!viewModel.TryCreatePurchasePopupData(itemData.StoreItemId, out PurchasePopupViewData popupData))
        {
            return;
        }

        purchasePopup.Bind(popupData, OnPurchaseConfirmed, null);
        purchasePopup.Show();
    }

    void OnPurchaseConfirmed(PurchasePopupViewData popupData, int count)
    {
        Debug.Log($"Purchase store item: {popupData.StoreItemId}, count: {count}");
        purchasePopup.Hide();
    }
}
