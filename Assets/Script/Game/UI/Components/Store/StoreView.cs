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

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        isClosing = false;
        disposable.Clear();
        topView.Bind(OnCancel);
        viewModel = new StoreViewModel(GameContext.Instance.StoreDatabase, GameDatabase.ItemDatabase);
        tabView.Bind(viewModel);
        purchasePopup.HideImmediate();
        viewModel.CurrentTab
            .Subscribe(tab =>
            {
                topView.BindCurrencies(viewModel.GetVisibleCurrencyItemIds(tab));
                itemListView.Bind(viewModel.CreateItems(tab), OnStoreItemClicked);
            })
            .AddTo(disposable);

        ShowPanels().Forget();
    }

    public override void OnClose()
    {
        disposable.Clear();
        base.OnClose();
    }

    public override void OnCancel()
    {
        if (isClosing)
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
        inputBlock.SetActive(false);
    }

    async UniTask CloseWithAnimation()
    {
        inputBlock.SetActive(true);
        await UniTask.WhenAll(topPanel.Hide(), tabPanel.Hide(), itemAreaPanel.Hide());
        base.OnCancel();
    }

    void OnStoreItemClicked(StoreItemViewData itemData)
    {
        if (!viewModel.TryCreatePurchasePopupData(itemData.StoreItemId, out PurchasePopupViewData popupData))
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
        if (!viewModel.TryPurchase(popupData.StoreItemId, count))
        {
            return;
        }

        purchasePopup.Hide();
    }
}
