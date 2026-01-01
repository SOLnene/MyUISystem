using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class ItemViewModel
{
    public readonly ReactiveProperty<string> name = new();
    public ReactiveProperty<string> desc = new();
    public readonly ReactiveProperty<int> star = new ReactiveProperty<int>();
    public readonly ReactiveProperty<string> iconPath = new ReactiveProperty<string>();
    public ReactiveProperty<Color> color = new();
    public ReactiveProperty<string> displayCountText = new();
    public ReactiveProperty<string> displayMainText = new();

    public InventoryItem Model { get; private set; }
    
    
    public ItemViewModel(InventoryItem item)
    {
        if (item == null)
        {
            Debug.LogError("ItemViewModel 构造函数传入item为空");
            return;
        }
        Model = item;
        SyncFormModel();
    }

    public virtual void SyncFormModel()
    {
        name.Value = Model.ItemName;
        desc = new ReactiveProperty<string>(Model.Desc);
        star.Value = Model.Stars;
        iconPath.Value = Model.IconPath;
        color.Value = RarityConfig.GetColor(Model.ItemRarity);
        displayCountText.Value = Model.GetDisplayLevelText();
        displayMainText.Value = Model.GetDisplayMainText();
    }
    
    protected virtual string UpdateDisplayCount()
    {
        return null;
    }

    // 当物品数据变化时调用
    public virtual void Refresh()
    {
        UpdateDisplayCount();
    }
}
