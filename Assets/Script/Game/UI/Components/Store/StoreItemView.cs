using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct StoreItemViewData
{
    public readonly string Id;
    public readonly string Name;
    public readonly Sprite Icon;
    public readonly Sprite CostIcon;
    public readonly int CostValue;
    public readonly int BeforeValue;
    public readonly int RemainCount;
    public readonly int DiscountPercent;
    public readonly Color BgColor;
    public readonly Color BottomColor;
    public readonly Color FrameColor;
    public readonly bool HasBeforeValue;
    public readonly bool HasRemainCount;
    public readonly bool HasDiscount;
    public readonly bool IsSoldOut;

    public StoreItemViewData(
        string id,
        string name,
        Sprite icon,
        Sprite costIcon,
        int costValue,
        Color bgColor,
        Color bottomColor,
        Color frameColor,
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
        Icon = icon;
        CostIcon = costIcon;
        CostValue = costValue;
        BeforeValue = beforeValue;
        RemainCount = remainCount;
        DiscountPercent = discountPercent;
        BgColor = bgColor;
        BottomColor = bottomColor;
        FrameColor = frameColor;
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
    Image bottomBg;
    [SerializeField]
    Image costIcon;
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

    public void Bind(StoreItemViewData viewData, Action<StoreItemViewData> clickHandler)
    {
        data = viewData;
        onClicked = clickHandler;

        bg.color = data.BgColor;
        bottomBg.color = data.BottomColor;

        icon.sprite = data.Icon;
        costIcon.sprite = data.CostIcon;
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

    void OnDestroy()
    {
        button.onClick.RemoveListener(HandleClick);
    }
}
