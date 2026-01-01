using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class BackpackTopViewModel
{
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