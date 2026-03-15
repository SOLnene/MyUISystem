using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackpackModel
{
    private readonly List<InventoryItem> _items = new List<InventoryItem>();

    public IReadOnlyList<InventoryItem> Items => _items;

    public void AddItem(InventoryItem inventoryItem) => _items.Add(inventoryItem);

    public void RemoveItem(InventoryItem inventoryItem) => _items.Remove(inventoryItem);
}
