using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class BackpackMiddleViewModel
{
    public readonly ReactiveCollection<ItemSlotViewModel> displaySlots = new ReactiveCollection<ItemSlotViewModel>();

    
    
    public readonly BackpackViewModel backpackVM;

    //管理绑定生命周期
    private readonly CompositeDisposable disposables = new CompositeDisposable();
    
    ItemCategory currentCategory;
    
    public BackpackMiddleViewModel(BackpackViewModel vm)
    {
        backpackVM = vm;
        
        vm.SlotsViewModels.ObserveAdd().Subscribe(_ => UpdateDisplayItems()).AddTo(disposables);
        vm.SlotsViewModels.ObserveRemove().Subscribe(_ => UpdateDisplayItems()).AddTo(disposables);
        UpdateDisplayItems();
    }

    public void SelectItem(ItemSlotViewModel slot)
    {
        ApplySelection(slot);
        backpackVM.MarkSeen(slot);
    }

    internal void PreviewItem(ItemSlotViewModel slot)
    {
        // 分类切换和删除后的自动预览不代表玩家主动查看，不能清除红点。
        ApplySelection(slot);
    }
    
    public void FilterByCategory(ItemCategory category)
    {
        currentCategory = category;
        UpdateDisplayItems();
        if (displaySlots.Count > 0)
        {
            PreviewItem(displaySlots[0]);
        }
    }

    void UpdateDisplayItems()
    {
        displaySlots.Clear();
        foreach (var slot in backpackVM.SlotsViewModels)
        {
            var item = slot.ItemViewModel;
            if (item.Model.ItemDefinition.category == currentCategory || currentCategory == ItemCategory.All)
            {
                displaySlots.Add(slot);
            }
        }
    }
    
    void ApplySelection(ItemSlotViewModel clickedVM)
    {
        foreach (var slot in displaySlots)
        {
            slot.isSelected.Value = slot == clickedVM;
        }
        backpackVM.SelectItem(clickedVM);
    }
    
    public void Dispose()
    {
        disposables.Dispose();
    }
}
