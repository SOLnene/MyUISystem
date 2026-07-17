using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class  BackpackViewModel
{
    readonly InventoryRepository inventoryRepository;
    /// <summary>
    /// 外部接口
    /// </summary>
    public IReadOnlyList<InventoryItem> allItems => inventoryRepository.GetAllItems();
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
    
    public BackpackViewModel(InventoryRepository inventoryRepository)
    {
        this.inventoryRepository = inventoryRepository;

        foreach (var item in inventoryRepository.GetAllItems())
        {
            CreateSlotVM(item);
        }
        inventoryRepository.ObserveChanged()
            .Subscribe(OnInventoryChanged)
            .AddTo(disposables);

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
        inventoryRepository.AddItem(inventoryItem);
        GameSaveCoordinator.Instance.MarkDirty();
    }
    
    public void RemoveItem(InventoryItem inventoryItem)
    {
        inventoryRepository.RemoveItem(inventoryItem);
        GameSaveCoordinator.Instance.MarkDirty();
    }

    void OnInventoryChanged(InventoryChangedEvent changeEvent)
    {
        switch (changeEvent.Type)
        {
            case InventoryChangeType.Added:
                CreateSlotVM(changeEvent.Item);
                break;
            case InventoryChangeType.StackChanged:
                RefreshSlotVM(changeEvent.Item);
                break;
            case InventoryChangeType.Removed:
                RemoveSlotVM(changeEvent.Item);
                break;
        }
    }

    void RefreshSlotVM(InventoryItem inventoryItem)
    {
        if (itemToSlotVM.TryGetValue(inventoryItem, out ItemSlotViewModel slotVM))
        {
            slotVM.ItemViewModel.Refresh();
        }
    }

    void RemoveSlotVM(InventoryItem inventoryItem)
    {
        if (!itemToSlotVM.TryGetValue(inventoryItem, out var slotVM))
        {
            return;
        }

        bool wasSelected = selectedSlot.Value == slotVM;
        SlotsViewModels.Remove(slotVM);
        itemToSlotVM.Remove(inventoryItem);
        slotVM.Dispose();

        if (wasSelected)
        {
            middleVM.SelectItem(middleVM.displaySlots.Count > 0
                ? middleVM.displaySlots[0]
                : null);
        }
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
