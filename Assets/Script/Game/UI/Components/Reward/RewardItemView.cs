using TMPro;
using UnityEngine;

public class RewardItemView : MonoBehaviour
{
    [SerializeField]
    ItemSlotView itemSlot;
    [SerializeField]
    TextMeshProUGUI itemNameText;

    ItemSlotViewModel itemSlotViewModel;

    public void Bind(RewardItemDisplayData data)
    {
        itemSlotViewModel ??= new ItemSlotViewModel();
        itemSlotViewModel.isEmpty.Value = false;
        itemSlotViewModel.iconPath.Value = data.IconAddress;
        itemSlotViewModel.count.Value = data.Amount.ToString();
        itemSlotViewModel.color.Value = RarityConfig.GetColor(data.Rarity);
        itemSlotViewModel.star.Value = data.Star;

        itemNameText.text = data.ItemName;
        itemSlot.Bind(itemSlotViewModel);
    }

    public void Clear()
    {
        itemSlot.ResetState();
        itemSlotViewModel?.Dispose();
        itemSlotViewModel = null;
        itemNameText.text = string.Empty;
    }

    void OnDestroy()
    {
        Clear();
    }
}
