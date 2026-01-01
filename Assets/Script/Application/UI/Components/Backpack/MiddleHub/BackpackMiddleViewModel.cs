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

        foreach (var slot in vm.SlotsViewModels)
        {
            slot.onClick.Subscribe(_ => OnSlotClicked(slot)).AddTo(disposables);
        }
        UpdateDisplayItems();
    }

    public void SelectItem(ItemSlotViewModel slot)
    {
        backpackVM.selectedSlot.Value = slot;
    }
    
    public void FilterByCategory(ItemCategory category)
    {
        currentCategory = category;
        UpdateDisplayItems();
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
    
    void OnSlotClicked(ItemSlotViewModel clickedVM)
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
