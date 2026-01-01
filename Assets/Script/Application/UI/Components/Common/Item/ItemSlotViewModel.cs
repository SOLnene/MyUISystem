using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;


public class ItemSlotViewModel: IDisposable
{
    //todo:转发itemviewmodel和其子类
    public readonly ReactiveProperty<string> iconPath = new ReactiveProperty<string>();
    public readonly ReactiveProperty<string> count = new ReactiveProperty<string>();
    public readonly ReactiveProperty<bool> isSelected = new ReactiveProperty<bool>(false);
    public readonly ReactiveProperty<int> star = new ReactiveProperty<int>();
    public readonly ReactiveProperty<Color> color = new();
    public readonly ReactiveProperty<bool> isEmpty = new ReactiveProperty<bool>(true);
    /// <summary>
    /// 用于素材选择界面
    /// </summary>
    public readonly ReactiveProperty<bool> isChecked = new ReactiveProperty<bool>(false);
    public ItemViewModel ItemViewModel { get; private set; }
    
    public readonly ReactiveCommand onClick = new ReactiveCommand();
    /// <summary>
    /// 用于素材选择界面，移除选择按钮
    /// </summary>
    public readonly ReactiveCommand onRemove = new();
    
    CompositeDisposable disposables = new CompositeDisposable();

    public ItemSlotViewModel()
    {
        ItemViewModel = null;
        isEmpty.Value = true;
        star.Value = 0;
        SetItem(null);
    }
    
    public ItemSlotViewModel(InventoryItem item)
    {
        //ItemViewModel = new ItemViewModel(item);
        SetItem(item);
    }
    
    public void SetItem(InventoryItem item)
    {
        disposables.Clear();

        if (item == null)
        {
            ItemViewModel = null;
            isEmpty.Value = true;
            star.Value = 0;
            color.Value = RarityConfig.GetColor(ItemRarity.Common);
            SetAddIconPath();
            return;
        }

        ItemViewModel = new ItemViewModel(item);
        isEmpty.Value = false;

        // 订阅
        ItemViewModel.displayCountText.Subscribe(text=> count.Value = text).AddTo(disposables);
        ItemViewModel.star.Subscribe(s => star.Value = s).AddTo(disposables); 
        ItemViewModel.iconPath.Subscribe(path => iconPath.Value = path).AddTo(disposables);
        ItemViewModel.color.Subscribe(c => color.Value = c).AddTo(disposables);
    }
    
    public void SetSelected(bool selected)
    {
        isSelected.Value = selected;
    }

    public void ClearItem()
    {
        SetItem(null);
    }
    
    public void Dispose()
    {
        disposables.Dispose();
    }

    /// <summary>
    /// 设置iconpath为加号对应的路径
    /// </summary>
    void SetAddIconPath()
    {
        iconPath.Value = "Assets/AssetsPackage/UI/Sprite/TouchIcon/UI_TouchIcon_Plus.png";
    }
}
