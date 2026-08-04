using TMPro;
using UnityEngine;

public class RewardItemView : MonoBehaviour
{
    [SerializeField]
    ItemSlotView itemSlot;
    [SerializeField]
    TextMeshProUGUI itemNameText;
    [SerializeField]
    AnimatedPanel animatedPanel;

    public AnimatedPanel AnimationPanel => animatedPanel;

    public void Bind(
        ItemDefinition itemDefinition,
        ItemSlotViewModel itemSlotViewModel)
    {
        itemNameText.text = itemDefinition.itemName;
        itemSlot.Bind(itemSlotViewModel);
    }

    public void Clear()
    {
        itemSlot.ResetState();
        itemNameText.text = string.Empty;
    }
}
