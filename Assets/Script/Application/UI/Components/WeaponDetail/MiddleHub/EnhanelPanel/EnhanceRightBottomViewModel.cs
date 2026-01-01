using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;

public class EnhanceRightBottomViewModel: IDisposable
{
    public readonly ReactiveCollection<ItemSlotViewModel> slotViewModels = new();

    public readonly ReactiveProperty<int> currentConsume = new();
    public readonly ReactiveProperty<int> totalExp = new();
    public readonly ReactiveProperty<int> totalGoldCost = new();
    
    public readonly int maxConsume = 40;

    public List<int> availabelLevels = new List<int>(){1,2,3};
    int starLimit = 1;
    
    readonly MaterialSelectService selectService;

    public readonly ReactiveProperty<EquipItemViewModel> vm;
    
    CompositeDisposable disposables = new CompositeDisposable();
    
    public EnhanceRightBottomViewModel(ReactiveProperty<EquipItemViewModel> viewModel)
    {
        vm = viewModel;
        
        selectService = new MaterialSelectService(maxConsume);
        if (slotViewModels.Count == 0)
        {
            for (int i = 0; i < maxConsume; i++)
            {
                slotViewModels.Add(new ItemSlotViewModel());
            }
        }
        selectService.SelectedItems.ObserveCountChanged().Subscribe(_ =>
        {
            var items = selectService.SelectedItems;
            currentConsume.Value = items.Count;
            int total = 0;
            foreach (var item in items)
            {
                if (item is EquipItem equip)
                    total += equip.GetExpValue();
            }
            var preview = vm.Value.Model.GetPreviewWithExp(total);
            totalExp.Value = preview.maxGainExp;
            totalGoldCost.Value = preview.costGold;
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

    public void FilterByLevel(int level)
    {
        starLimit = level;
        //todo:根据等级过滤
    }

    public void OnQuickAddClicked()
    {
        var allItems = GameContext.Instance.InventoryRepository.GetAllItems();
    
        // 根据当前筛选星数过滤
        var filter = new ItemFilter(ItemCategory.Equip, starLimit);
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

        Debug.Log($"[QuickAdd] 自动添加 {addedCount} 个材料 (星级 ≤ {starLimit})");
    }
    

    public void OnSlotClick(ItemSlotViewModel viewModel)
    {
        int index = slotViewModels.IndexOf(viewModel);
        
        MaterialSelectParams materialSelectParams = new MaterialSelectParams(index,new ItemFilter(ItemCategory.Equip,starLimit),maxConsume,selectService);
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

    /// <summary>
    /// 在移除某个物品后，让所有非空的 Slot 向前移动，空的补到末尾
    /// </summary>
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
    
    public void Dispose()
    {
        disposables.Dispose();
    }
}
