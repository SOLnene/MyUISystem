using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct PurchasePopupViewData
{
    public readonly int StoreItemId;
    public readonly string ItemName;
    public readonly string ItemIconPath;
    public readonly string CostIconPath;
    public readonly int ItemCount;
    public readonly int UnitPrice;
    public readonly int MaxPurchaseCount;

    public PurchasePopupViewData(
        int storeItemId,
        string itemName,
        string itemIconPath,
        string costIconPath,
        int itemCount,
        int unitPrice,
        int maxPurchaseCount)
    {
        StoreItemId = storeItemId;
        ItemName = itemName;
        ItemIconPath = itemIconPath;
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
    Image costIcon;
    [SerializeField]
    TextMeshProUGUI costValueText;
    [SerializeField]
    Button cancelButton;
    [SerializeField]
    Button confirmButton;

    PurchasePopupViewData data;
    Action<PurchasePopupViewData, int> onConfirm;
    Action onCancel;
    int purchaseCount = 1;
    int itemIconRequestVersion;
    CancellationTokenSource itemIconRequestCts;
    int costIconRequestVersion;
    CancellationTokenSource costIconRequestCts;

    public void Bind(
        PurchasePopupViewData viewData,
        Action<PurchasePopupViewData, int> confirmHandler,
        Action cancelHandler)
    {
        data = viewData;
        onConfirm = confirmHandler;
        onCancel = cancelHandler;
        purchaseCount = 1;

        itemNameText.text = data.ItemName;
        LoadItemIcon(data.ItemIconPath);
        LoadCostIcon(data.CostIconPath);
        RefreshCount();

        countScrollbar.onValueChanged.RemoveListener(HandleCountChanged);
        countScrollbar.onValueChanged.AddListener(HandleCountChanged);
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
        costValueText.text = (data.UnitPrice * purchaseCount).ToString();
        countScrollbar.size = data.MaxPurchaseCount <= 1 ? 1 : 0;
        countScrollbar.value = data.MaxPurchaseCount <= 1 ? 0 : Mathf.InverseLerp(1, data.MaxPurchaseCount, purchaseCount);
        countScrollbar.interactable = data.MaxPurchaseCount > 1;
    }

    void HandleCancel()
    {
        Hide();
        onCancel?.Invoke();
    }

    void HandleConfirm()
    {
        onConfirm?.Invoke(data, purchaseCount);
    }

    void LoadItemIcon(string iconPath)
    {
        itemIconRequestVersion++;
        itemIconRequestCts?.Cancel();
        itemIconRequestCts?.Dispose();
        itemIconRequestCts = null;

        itemIcon.sprite = null;
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        itemIconRequestCts = new CancellationTokenSource();
        LoadItemIconAsync(iconPath, itemIconRequestVersion, itemIconRequestCts.Token).Forget();
    }

    async UniTask LoadItemIconAsync(string iconPath, int requestVersion, CancellationToken cancellationToken)
    {
        Sprite sprite;
        try
        {
            sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(iconPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || requestVersion != itemIconRequestVersion || data.ItemIconPath != iconPath)
        {
            return;
        }

        itemIcon.sprite = sprite;
    }

    void LoadCostIcon(string iconPath)
    {
        costIconRequestVersion++;
        costIconRequestCts?.Cancel();
        costIconRequestCts?.Dispose();
        costIconRequestCts = null;

        costIcon.sprite = null;
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        costIconRequestCts = new CancellationTokenSource();
        LoadCostIconAsync(iconPath, costIconRequestVersion, costIconRequestCts.Token).Forget();
    }

    async UniTask LoadCostIconAsync(string iconPath, int requestVersion, CancellationToken cancellationToken)
    {
        Sprite sprite;
        try
        {
            sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(iconPath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || requestVersion != costIconRequestVersion || data.CostIconPath != iconPath)
        {
            return;
        }

        costIcon.sprite = sprite;
    }

    void OnDestroy()
    {
        itemIconRequestCts?.Cancel();
        itemIconRequestCts?.Dispose();
        costIconRequestCts?.Cancel();
        costIconRequestCts?.Dispose();
        countScrollbar.onValueChanged.RemoveListener(HandleCountChanged);
        cancelButton.onClick.RemoveListener(HandleCancel);
        confirmButton.onClick.RemoveListener(HandleConfirm);
    }
}
