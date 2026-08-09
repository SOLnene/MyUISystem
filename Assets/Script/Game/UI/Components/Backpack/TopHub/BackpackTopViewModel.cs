using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class BackpackTopViewModel
{
    readonly InventoryRepository inventoryRepository;
    public readonly ReactiveProperty<int> SelectedCategoryIndex = new ReactiveProperty<int>();
    public readonly List<ItemCategory> Categories;

    public ItemCategory CurrentCategory => Categories[SelectedCategoryIndex.Value];
    
    public TopViewType TopViewType { get; private set; }

    public BackpackTopViewModel(TopViewType type)
    {
        TopViewType = type;
    }
    
    public BackpackTopViewModel(List<ItemCategory> categories,int defaultIndex = 0)
    {
        Categories = categories;
        SelectedCategoryIndex.Value = defaultIndex;
    }

    internal BackpackTopViewModel(
        List<ItemCategory> categories,
        InventoryRepository inventoryRepository,
        int defaultIndex = 0)
    {
        Categories = categories;
        this.inventoryRepository = inventoryRepository;
        SelectedCategoryIndex.Value = defaultIndex;
    }

    internal IReadOnlyReactiveProperty<bool> ObserveHasUnseen(ItemCategory category)
    {
        return inventoryRepository.ObserveHasUnseen(category);
    }
    
    public void SetCategory(int index)
    {
        if (TopViewType != TopViewType.Backpack)
        {
            return;
        }
        if (index >= 0 && index < Categories.Count)
        {
            SelectedCategoryIndex.Value = index;
        }
    }
}

public enum TopViewType
{
    Backpack,
    WeaponDetail,
}
