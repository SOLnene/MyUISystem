using System.Collections.Generic;

public class PromoteMaterialPreviewViewModel
{
    public readonly List<ItemSlotViewModel> itemSlotViewModels = new();

    public void SetMaterials(IEnumerable<PromoteMaterialCost> costs)
    {
        itemSlotViewModels.Clear();

        if (costs == null)
            return;

        foreach (var cost in costs)
        {
            AddMaterial(cost.materialKey, cost.count);
        }
    }

    void AddMaterial(string materialKey, int count)
    {
        var item = ItemFactory.CreateItem(materialKey) as MaterialItem;
        if (item == null)
            return;

        item.SetNeeded(count);
        itemSlotViewModels.Add(new ItemSlotViewModel(item));
    }
}
