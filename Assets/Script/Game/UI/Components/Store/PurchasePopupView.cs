using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct PurchasePopupViewData
{
    public readonly int StoreItemId;
    public readonly ItemDefinition ItemDefinition;
    public readonly string ItemName;
    public readonly string ItemIconPath;
    public readonly string CostIconPath;
    public readonly int ItemCount;
    public readonly int UnitPrice;
    public readonly int MaxPurchaseCount;

    public PurchasePopupViewData(
        int storeItemId,
        ItemDefinition itemDefinition,
        string costIconPath,
        int itemCount,
        int unitPrice,
        int maxPurchaseCount)
    {
        StoreItemId = storeItemId;
        ItemDefinition = itemDefinition;
        ItemName = itemDefinition != null ? itemDefinition.itemName : string.Empty;
        ItemIconPath = itemDefinition?.iconPath;
        CostIconPath = costIconPath;
        ItemCount = itemCount;
        UnitPrice = unitPrice;
        MaxPurchaseCount = Mathf.Max(1, maxPurchaseCount);
    }
}

public class PurchasePopupView : MonoBehaviour
{
    [SerializeField]
    Image itemIcon;
    [SerializeField]
    TextMeshProUGUI itemNameText;
    [SerializeField]
    TextMeshProUGUI countValueText;
    [SerializeField]
    Scrollbar countScrollbar;
    [SerializeField]
    ResourceAmountView costAmountView;
    [SerializeField]
    Button itemAreaButton;
    [SerializeField]
    Button cancelButton;
    [SerializeField]
    Button confirmButton;

    PurchasePopupViewData data;
    Action<PurchasePopupViewData, int> onConfirm;
    Action<ItemDefinition> onItemAreaClicked;
    Action onCancel;
    int purchaseCount = 1;
    CancellationTokenSource itemIconRequestCts;

    public void Bind(
        PurchasePopupViewData viewData,
        Action<PurchasePopupViewData, int> confirmHandler,
        Action cancelHandler,
        Action<ItemDefinition> itemAreaClickHandler = null)
    {
        data = viewData;
        onConfirm = confirmHandler;
        onCancel = cancelHandler;
        onItemAreaClicked = itemAreaClickHandler;
        purchaseCount = 1;

        itemNameText.text = data.ItemName;
        itemIconRequestCts = IconLoader.LoadSpriteAsync(itemIcon, data.ItemIconPath, this, itemIconRequestCts);
        costAmountView.Bind(data.CostIconPath, data.UnitPrice);
        RefreshCount();

        countScrollbar.onValueChanged.RemoveListener(HandleCountChanged);
        countScrollbar.onValueChanged.AddListener(HandleCountChanged);
        itemAreaButton.onClick.RemoveListener(HandleItemAreaClicked);
        itemAreaButton.onClick.AddListener(HandleItemAreaClicked);
        cancelButton.onClick.RemoveListener(HandleCancel);
        cancelButton.onClick.AddListener(HandleCancel);
        confirmButton.onClick.RemoveListener(HandleConfirm);
        confirmButton.onClick.AddListener(HandleConfirm);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void HandleCountChanged(float value)
    {
        int max = Mathf.Max(1, data.MaxPurchaseCount);
        purchaseCount = max <= 1 ? 1 : Mathf.RoundToInt(Mathf.Lerp(1, max, value));
        RefreshCount();
    }

    void RefreshCount()
    {
        countValueText.text = purchaseCount.ToString();
        costAmountView.SetAmount(data.UnitPrice * purchaseCount);
        countScrollbar.numberOfSteps = data.MaxPurchaseCount;
        countScrollbar.value = data.MaxPurchaseCount <= 1 ? 0 : Mathf.InverseLerp(1, data.MaxPurchaseCount, purchaseCount);
    }

    void HandleCancel()
    {
        Hide();
        onCancel?.Invoke();
    }

    void HandleItemAreaClicked()
    {
        onItemAreaClicked?.Invoke(data.ItemDefinition);
    }

    void HandleConfirm()
    {
        onConfirm?.Invoke(data, purchaseCount);
    }

    void OnDestroy()
    {
        IconLoader.Cancel(itemIconRequestCts);
        countScrollbar.onValueChanged.RemoveListener(HandleCountChanged);
        itemAreaButton.onClick.RemoveListener(HandleItemAreaClicked);
        cancelButton.onClick.RemoveListener(HandleCancel);
        confirmButton.onClick.RemoveListener(HandleConfirm);
    }
}
