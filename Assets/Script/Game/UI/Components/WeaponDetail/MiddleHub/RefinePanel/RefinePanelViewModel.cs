using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public class RefinePanelViewModel
{
    public readonly ReactiveProperty<EquipItemViewModel> equipItem;
    public readonly ReactiveProperty<int> previewCost = new();
    
    
    CompositeDisposable disposables = new CompositeDisposable();
    
    public readonly ReactiveCollection<ItemSlotViewModel> slotViewModels = new();
    
    static int maxConsume = 4;
    
    MaterialSelectService selectService;
    
    public RefinePanelViewModel(ReactiveProperty<EquipItemViewModel> viewModel)
    {
        equipItem = viewModel;

        for (int i = 0; i < maxConsume; i++)
        {
            slotViewModels.Add(new ItemSlotViewModel());
        }
        
        selectService = new MaterialSelectService(maxConsume);
        
        selectService.SelectedItems.ObserveCountChanged().Subscribe(_ =>
        {
            previewCost.Value = equipItem.Value.Model.GetRefineCost(selectService.SelectedItems.Count);
            
        }).AddTo(disposables);
        selectService.OnDelta.Subscribe(delta =>
        {
            if (delta.added)
            {
                AddToFirstEmptySlot(delta.item);
            }
            else
            {
                RemoveFromLastMatchedSlot(delta.item);
            }
        }).AddTo(disposables);
    }
    
    public void OnQuickAddClicked()
    {
        var allItems = GameContext.Instance.InventoryRepository.GetAllItems();
    
        // 根据当前筛选星数过滤
        var filter = new ItemFilter(ItemCategory.Equip, (int)ItemRarity.Max,equipItem.Value.Model.Id);
        var filteredItems = allItems.Where(item => filter.Match(item));
    
        int addedCount = 0;

        foreach (var item in filteredItems)
        {
            // 已满则停止
            if (addedCount >= maxConsume)
                break;

            // 跳过已选择的物品
            if (selectService.Contains(item))
                continue;

            // 使用已有逻辑尝试选择（会自动触发 AddToFirstEmptySlot）
            bool success = selectService.TrySelect(item);
            if (success)
            {
                addedCount++;
            }
        }
        
    }

    public void OnSlotClick(ItemSlotViewModel viewModel)
    {
        int index = slotViewModels.IndexOf(viewModel);
        Debug.Log(index);
        MaterialSelectParams materialSelectParams = new MaterialSelectParams(index,new ItemFilter(ItemCategory.Equip,(int)ItemRarity.Max,equipItem.Value.Model.Id),
            maxConsume,selectService);
        UIManager.Instance.Open(UIType.ItemSelectPopupView, materialSelectParams);
    }
    
    private void AddToFirstEmptySlot(InventoryItem item)
    {
        foreach (var s in slotViewModels)
        {
            if (s.ItemViewModel == null)
            {
                s.SetItem(item);
                return;
            }
        }
    }

    private void RemoveFromLastMatchedSlot(InventoryItem item)
    {
        for (int i = slotViewModels.Count - 1; i >= 0; i--)
        {
            if (slotViewModels[i].ItemViewModel!=null && slotViewModels[i].ItemViewModel.Model == item)
            {
                Debug.Log($"Remove item {item.ItemName} from slot {i}");
                slotViewModels[i].ClearItem();
                CompactSlots();
                return;
            }
        }
        
    }
    
    private void CompactSlots()
    {
        // 收集所有非空的 ItemViewModel
        List<ItemViewModel> nonEmptyItems = new List<ItemViewModel>();
        foreach (var slot in slotViewModels)
        {
            if (slot.ItemViewModel != null)
                nonEmptyItems.Add(slot.ItemViewModel);
        }

        // 重新分配到 slotViewModels
        for (int i = 0; i < slotViewModels.Count; i++)
        {
            if (i < nonEmptyItems.Count)
            {
                // 重新设置已有物品
                slotViewModels[i].SetItem(nonEmptyItems[i].Model);
            }
            else
            {
                // 剩下的槽清空并显示加号图标
                slotViewModels[i].ClearItem();
            }
        }
    }
}
