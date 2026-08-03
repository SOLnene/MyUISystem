using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
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
    CurrencyValueView costAmountView;
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
    [SerializeField]
    AnimatedPanel anim;

    StoreItemViewModel viewModel;
    Action<StoreItemViewModel> onClicked;
    readonly CompositeDisposable bindDisposables = new();
    CancellationTokenSource itemIconRequestCts;

    internal AnimatedPanel Anim => anim;

    public void Bind(StoreItemViewModel itemViewModel, Action<StoreItemViewModel> clickHandler)
    {
        bindDisposables.Clear();
        viewModel = itemViewModel;
        onClicked = clickHandler;

        bg.color = viewModel.BackgroundColor;

        LoadItemIcon(viewModel.IconPath);
        costAmountView.Bind(viewModel.CostIconPath, viewModel.CostValue);
        nameText.text = viewModel.Name;

        beforeValue.gameObject.SetActive(viewModel.HasBeforeValue);
        if (viewModel.HasBeforeValue)
        {
            beforeValue.text = viewModel.BeforeValue.ToString();
        }

        discountArea.SetActive(viewModel.HasDiscount);
        if (viewModel.HasDiscount)
        {
            discountValue.text = $"-{viewModel.DiscountPercent}%";
        }

        viewModel.PurchasePreview
            .Subscribe(ApplyPurchasePreview)
            .AddTo(bindDisposables);

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    void HandleClick()
    {
        onClicked?.Invoke(viewModel);
    }

    void ApplyPurchasePreview(StorePurchasePreview preview)
    {
        remainText.gameObject.SetActive(viewModel.HasRemainCount);
        if (viewModel.HasRemainCount)
        {
            remainText.text = string.Format(remainFormat, preview.RemainingLimit);
        }

        button.interactable = preview.RemainingLimit > 0;
    }

    void LoadItemIcon(string iconPath)
    {
        itemIconRequestCts?.Cancel();
        itemIconRequestCts?.Dispose();
        itemIconRequestCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        IconLoader.SetSpriteAsync(icon, iconPath, itemIconRequestCts.Token).Forget();
    }

    void OnDestroy()
    {
        bindDisposables.Dispose();
        itemIconRequestCts?.Cancel();
        itemIconRequestCts?.Dispose();
        button.onClick.RemoveListener(HandleClick);
    }
}
