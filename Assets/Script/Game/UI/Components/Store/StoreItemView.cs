using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct StoreItemViewData
{
    public readonly int StoreItemId;
    public readonly string Id;
    public readonly string Name;
    public readonly string IconPath;
    public readonly string CostIconPath;
    public readonly int CostValue;
    public readonly int BeforeValue;
    public readonly int RemainCount;
    public readonly int DiscountPercent;
    public readonly Color BackgroundColor;
    public readonly bool HasBeforeValue;
    public readonly bool HasRemainCount;
    public readonly bool HasDiscount;
    public readonly bool IsSoldOut;

    public StoreItemViewData(
        int storeItemId,
        string id,
        string name,
        string iconPath,
        string costIconPath,
        int costValue,
        Color backgroundColor,
        int beforeValue = 0,
        int remainCount = 0,
        int discountPercent = 0,
        bool hasBeforeValue = false,
        bool hasRemainCount = false,
        bool hasDiscount = false,
        bool isSoldOut = false)
    {
        StoreItemId = storeItemId;
        Id = id;
        Name = name;
        IconPath = iconPath;
        CostIconPath = costIconPath;
        CostValue = costValue;
        BeforeValue = beforeValue;
        RemainCount = remainCount;
        DiscountPercent = discountPercent;
        BackgroundColor = backgroundColor;
        HasBeforeValue = hasBeforeValue;
        HasRemainCount = hasRemainCount;
        HasDiscount = hasDiscount;
        IsSoldOut = isSoldOut;
    }
}

public class StoreItemView : MonoBehaviour
{
    [SerializeField]
    Image bg;
    [SerializeField]
    Image icon;
    [SerializeField]
    TextMeshProUGUI nameText;
    [SerializeField]
    TextMeshProUGUI remainText;
    [SerializeField]
    ResourceAmountView costAmountView;
    [SerializeField]
    TextMeshProUGUI beforeValue;
    [SerializeField]
    GameObject discountArea;
    [SerializeField]
    TextMeshProUGUI discountValue;
    [SerializeField]
    Button button;
    [SerializeField]
    string remainFormat = "本月剩余数量:{0}";

    StoreItemViewData data;
    Action<StoreItemViewData> onClicked;
    int itemIconRequestVersion;
    CancellationTokenSource itemIconRequestCts;

    public void Bind(StoreItemViewData viewData, Action<StoreItemViewData> clickHandler)
    {
        data = viewData;
        onClicked = clickHandler;

        bg.color = data.BackgroundColor;

        LoadItemIcon(data.IconPath);
        costAmountView.Bind(data.CostIconPath, data.CostValue);
        nameText.text = data.Name;

        remainText.gameObject.SetActive(data.HasRemainCount);
        if (data.HasRemainCount)
        {
            remainText.text = string.Format(remainFormat, data.RemainCount);
        }

        beforeValue.gameObject.SetActive(data.HasBeforeValue);
        if (data.HasBeforeValue)
        {
            beforeValue.text = data.BeforeValue.ToString();
        }

        discountArea.SetActive(data.HasDiscount);
        if (data.HasDiscount)
        {
            discountValue.text = $"-{data.DiscountPercent}%";
        }

        button.interactable = !data.IsSoldOut;
        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    void HandleClick()
    {
        onClicked?.Invoke(data);
    }

    void LoadItemIcon(string iconPath)
    {
        itemIconRequestVersion++;
        itemIconRequestCts?.Cancel();
        itemIconRequestCts?.Dispose();
        itemIconRequestCts = null;

        icon.sprite = null;
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

        if (cancellationToken.IsCancellationRequested || requestVersion != itemIconRequestVersion || data.IconPath != iconPath)
        {
            return;
        }

        icon.sprite = sprite;
    }

    void OnDestroy()
    {
        itemIconRequestCts?.Cancel();
        itemIconRequestCts?.Dispose();
        button.onClick.RemoveListener(HandleClick);
    }
}
