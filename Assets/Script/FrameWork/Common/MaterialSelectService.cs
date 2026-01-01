using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class MaterialSelectService : IDisposable
{
    /*InventoryItem 属于「纯逻辑数据结构」，它不依赖任何 UI；
    ItemSlotViewModel、ItemViewModel 属于「表现层逻辑」，只在 UI 层用；
    Service（如 MaterialSelectService）属于「中间层」，负责协调不同 ViewModel 的交互。*/
    public readonly ReactiveCollection<InventoryItem> SelectedItems = new();
    public readonly Subject<MaterialDelta> OnDelta = new();
    
    private readonly int maxSelectCount;
    
    public MaterialSelectService(int maxSelectCount)
    {
        this.maxSelectCount = maxSelectCount;
    }

    public bool TrySelect(InventoryItem item)
    {
        if (SelectedItems.Contains(item))
        {
            return false;
        }
        if(SelectedItems.Count>= maxSelectCount)
        {
            return false;
        }
        SelectedItems.Add(item);
        OnDelta.OnNext(new MaterialDelta
        {
            item = item, added = true
        });
        return true;
    }

    public bool TryDeselect(InventoryItem item)
    {
        if (!SelectedItems.Contains(item))
        {
            return false;
        }
        SelectedItems.Remove(item);
        OnDelta.OnNext(new MaterialDelta(item, false));
        return true;
    }

    public bool Contains(InventoryItem item)
    {
        return SelectedItems.Contains(item);
    }
    
    public void Clear()
    {
        foreach (var item in SelectedItems)
        {
            OnDelta.OnNext(new MaterialDelta(item,false));
        }

        SelectedItems.Clear();
    }
    
    public void Dispose()
    {
        SelectedItems.Clear();
        OnDelta.Dispose();
    }
}


public struct MaterialDelta
{
    public InventoryItem item;
    /// <summary>
    /// 添加/移除
    /// </summary>
    public bool added;
    
    public MaterialDelta(InventoryItem item,bool added){
        this.item = item;
        this.added = added;
    }
}