using Cysharp.Threading.Tasks;
using UnityEngine;

public class PromoteMaterialPreviewView : MonoBehaviour
{
    [SerializeField]
    Transform materialParent;
    [SerializeField]
    MaterialAreaFeedbackView feedbackView;

    public async UniTask Bind(PromoteMaterialPreviewViewModel vm)
    {
        Clear();

        if (vm == null || materialParent == null)
            return;

        foreach (var materialVM in vm.itemSlotViewModels)
        {
            var itemSlotView = await ItemFactory.InstantiateItemSlot(
                materialVM,
                materialParent,
                "ui/prefab/item_slot_material",
                true);

            if (itemSlotView == null)
                continue;

            itemSlotView.Bind(materialVM);
        }
    }

    void Clear()
    {
        if (materialParent == null)
            return;

        foreach (Transform child in materialParent)
        {
            Destroy(child.gameObject);
        }
    }

    public void ShowProcessing()
    {
        feedbackView.ShowProcessing();
    }

    public void ShowNormal(bool playMaterialEnter)
    {
        feedbackView.ShowNormal(playMaterialEnter);
    }

    public void ShowResultText(string text)
    {
        feedbackView.ShowResultText(text);
    }
}
