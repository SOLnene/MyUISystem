using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct StoreItemViewData
{
    public readonly string Id;
    public readonly string Name;
    public readonly string IconPath;
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
        string id,
        string name,
        string iconPath,
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
        Id = id;
        Name = name;
        IconPath = iconPath;
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
    TextMeshProUGUI costValue;
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
    int iconRequestVersion;
    CancellationTokenSource iconRequestCts;

    public void Bind(StoreItemViewData viewData, Action<StoreItemViewData> clickHandler)
    {
        data = viewData;
        onClicked = clickHandler;

        bg.color = data.BackgroundColor;

        LoadIcon(data.IconPath);
        nameText.text = data.Name;
        costValue.text = data.CostValue.ToString();

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

    void LoadIcon(string iconPath)
    {
        iconRequestVersion++;
        iconRequestCts?.Cancel();
        iconRequestCts?.Dispose();
        iconRequestCts = null;

        icon.sprite = null;
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        iconRequestCts = new CancellationTokenSource();
        LoadIconAsync(iconPath, iconRequestVersion, iconRequestCts.Token).Forget();
    }

    async UniTask LoadIconAsync(string iconPath, int requestVersion, CancellationToken cancellationToken)
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

        if (cancellationToken.IsCancellationRequested || requestVersion != iconRequestVersion || data.IconPath != iconPath)
        {
            return;
        }

        icon.sprite = sprite;
    }

    void OnDestroy()
    {
        iconRequestCts?.Cancel();
        iconRequestCts?.Dispose();
        button.onClick.RemoveListener(HandleClick);
    }
}
