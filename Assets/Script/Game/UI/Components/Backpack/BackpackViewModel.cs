using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class  BackpackViewModel
{
    readonly BackpackModel model;
    /// <summary>
    /// 外部接口
    /// </summary>
    public IReadOnlyList<InventoryItem> allItems => model.Items;
    /*
     * 等价于
     * private readonly ReactiveCollection<ItemData> _items = new ReactiveCollection<ItemData>();

    public ReactiveCollection<ItemData> Items
    {
        get { return _items; }
    }
     */
    public ReactiveCollection<ItemSlotViewModel> SlotsViewModels { get; } = new();
    private readonly CompositeDisposable disposables = new CompositeDisposable();
    
    public readonly BackpackTopViewModel topVM;
    public readonly BackpackMiddleViewModel middleVM;
    public readonly InfoPanelViewModel infoVM;
    
    public readonly ReactiveProperty<ItemSlotViewModel> selectedSlot = new();
    
    //TODO:使用物品唯一id作为key
    public Dictionary<InventoryItem,ItemSlotViewModel> itemToSlotVM = new Dictionary<InventoryItem, ItemSlotViewModel>();
    
    public BackpackViewModel(BackpackModel backpackModel)
    {
        model = backpackModel;

        foreach (var item in model.Items)
        {
            CreateSlotVM(item);
        }
        var categories = new List<ItemCategory> { ItemCategory.Equip,ItemCategory.Consumable,ItemCategory.Material };
        topVM = new BackpackTopViewModel(categories);
        middleVM = new BackpackMiddleViewModel(this);
        infoVM = new InfoPanelViewModel();

        // 绑定事件
        topVM.SelectedCategoryIndex
            .Subscribe(_ =>
            {
                middleVM.FilterByCategory(topVM.CurrentCategory);
            })
            .AddTo(disposables);
        selectedSlot
            .Where(slot => slot != null)
            .Subscribe(slotVM => infoVM.Bind(slotVM.ItemViewModel))
            .AddTo(disposables);
    }
    
    public void AddItem(InventoryItem inventoryItem)
    {
        model.AddItem(inventoryItem);
        CreateSlotVM(inventoryItem);
    }
    
    public void RemoveItem(InventoryItem inventoryItem)
    {
        model.RemoveItem(inventoryItem);
        SlotsViewModels.Remove(itemToSlotVM[inventoryItem]);
    }

    /// <summary>
    /// 创建slotVM并添加到Items中和映射表中
    /// </summary>
    /// <param name="???"></param>
    void CreateSlotVM(InventoryItem item)
    {
        var slotVM = new ItemSlotViewModel(item);
        SlotsViewModels.Add(slotVM);
        itemToSlotVM.Add(item,slotVM);
    }
    
    public void SelectItem(ItemSlotViewModel inventoryItem)
    {
        selectedSlot.Value = inventoryItem;
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
