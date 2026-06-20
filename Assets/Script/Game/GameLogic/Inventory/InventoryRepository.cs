using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class InventoryRepository
{
    private readonly List<InventoryItem> items = new List<InventoryItem>();
    private readonly Dictionary<long, InventoryItem> itemsByInstanceId = new Dictionary<long, InventoryItem>();
    private readonly Dictionary<int, ItemStack> itemStacks = new Dictionary<int, ItemStack>();
    private readonly Subject<InventoryChangedEvent> changed = new Subject<InventoryChangedEvent>();
    private long nextInstanceId = 1;

    public IReadOnlyList<InventoryItem> GetAllItems() => items;

    public IEnumerable<ItemStack> GetAllStacks() => itemStacks.Values;

    public IObservable<InventoryChangedEvent> ObserveChanged()
    {
        return changed;
    }

    public int GetItemCount(int itemId)
    {
        if (itemStacks.TryGetValue(itemId, out ItemStack stack))
        {
            return stack.Count;
        }

        int count = 0;
        foreach (InventoryItem item in items)
        {
            if (item.Id == itemId)
            {
                count++;
            }
        }

        return count;
    }

    public void AddItem(InventoryItem inventoryItem)
    {
        if (inventoryItem is IStackableItem stackableItem)
        {
            AddStackItem(inventoryItem, stackableItem.Count);
            return;
        }

        inventoryItem.SetInstanceId(nextInstanceId);
        nextInstanceId++;
        items.Add(inventoryItem);
        itemsByInstanceId.Add(inventoryItem.InstanceId, inventoryItem);
        changed.OnNext(new InventoryChangedEvent(InventoryChangeType.Added, inventoryItem));
    }

    void AddStackItem(InventoryItem inventoryItem, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (itemStacks.TryGetValue(inventoryItem.Id, out ItemStack stack))
        {
            stack.Add(amount);
            InventoryItem existingItem = FindStackItem(inventoryItem.Id);
            if (existingItem is IStackableItem existingStackable)
            {
                existingStackable.Add(amount);
                changed.OnNext(new InventoryChangedEvent(InventoryChangeType.StackChanged, existingItem, stack));
            }

            return;
        }

        stack = new ItemStack(inventoryItem.ItemDefinition, amount);
        itemStacks.Add(inventoryItem.Id, stack);
        items.Add(inventoryItem);
        changed.OnNext(new InventoryChangedEvent(InventoryChangeType.Added, inventoryItem, stack));
    }

    InventoryItem FindStackItem(int itemId)
    {
        foreach (InventoryItem item in items)
        {
            if (item.Id == itemId && item is IStackableItem)
            {
                return item;
            }
        }

        return null;
    }

    public void RemoveItem(InventoryItem inventoryItem)
    {
        items.Remove(inventoryItem);
        itemsByInstanceId.Remove(inventoryItem.InstanceId);
        if (inventoryItem is IStackableItem)
        {
            itemStacks.Remove(inventoryItem.Id);
        }

        changed.OnNext(new InventoryChangedEvent(InventoryChangeType.Removed, inventoryItem));
    }

    public bool TryGetItem(long instanceId, out InventoryItem item)
    {
        if (instanceId <= 0)
        {
            item = null;
            return false;
        }

        return itemsByInstanceId.TryGetValue(instanceId, out item);
    }

    public bool TryGetEquip(long instanceId, out EquipItem equip)
    {
        if (TryGetItem(instanceId, out var item) && item is EquipItem equipItem)
        {
            equip = equipItem;
            return true;
        }

        equip = null;
        return false;
    }

    public IEnumerable<InventoryItem> GetItemsByKey(string key)
    {
        foreach (var item in items)
        {
            if (item.Key == key)
            {
                yield return item;
            }
        }
    }

    public IEnumerable<EquipItem> GetAllEquips()
    {
        foreach (var item in items)
        {
            if (item is EquipItem equip)
            {
                yield return equip;
            }
        }
    }

    public InventorySaveData ExportSaveData()
    {
        InventorySaveData saveData = new InventorySaveData();
        foreach (ItemStack stack in itemStacks.Values)
        {
            saveData.stacks.Add(new ItemStackSaveData(stack.ItemId, stack.Count));
        }

        Dictionary<int, int> nonStackCounts = new Dictionary<int, int>();
        foreach (InventoryItem item in items)
        {
            if (item is IStackableItem)
            {
                continue;
            }

            if (item is EquipItem equipItem)
            {
                saveData.equips.Add(new EquipItemSaveData(
                    equipItem.InstanceId,
                    equipItem.Id,
                    equipItem.Level,
                    equipItem.CurrentExp,
                    equipItem.Rank,
                    equipItem.RefinementLevel));
                continue;
            }

            nonStackCounts.TryGetValue(item.Id, out int count);
            nonStackCounts[item.Id] = count + 1;
        }

        foreach (KeyValuePair<int, int> pair in nonStackCounts)
        {
            saveData.stacks.Add(new ItemStackSaveData(pair.Key, pair.Value));
        }

        return saveData;
    }

    public void ImportSaveData(InventorySaveData saveData)
    {
        Clear();
        if (saveData == null)
        {
            changed.OnNext(new InventoryChangedEvent(InventoryChangeType.Reset, null));
            return;
        }

        if (saveData.stacks != null)
        {
            foreach (ItemStackSaveData stackData in saveData.stacks)
            {
                ImportStack(stackData);
            }
        }

        if (saveData.equips != null)
        {
            foreach (EquipItemSaveData equipData in saveData.equips)
            {
                ImportEquip(equipData);
            }
        }

        changed.OnNext(new InventoryChangedEvent(InventoryChangeType.Reset, null));
    }

    void Clear()
    {
        items.Clear();
        itemsByInstanceId.Clear();
        itemStacks.Clear();
        nextInstanceId = 1;
    }

    void ImportStack(ItemStackSaveData stackData)
    {
        if (stackData == null || stackData.count <= 0)
        {
            return;
        }

        ItemDefinition itemDefinition = GameDatabase.ItemDatabase.GetItemByID(stackData.itemId);
        if (itemDefinition == null)
        {
            Debug.LogWarning($"读取背包堆叠物品失败，找不到物品: {stackData.itemId}");
            return;
        }

        InventoryItem item = CreateStackDisplayItem(itemDefinition, stackData.count);
        itemStacks[itemDefinition.id] = new ItemStack(itemDefinition, stackData.count);
        items.Add(item);
    }

    InventoryItem CreateStackDisplayItem(ItemDefinition itemDefinition, int count)
    {
        return itemDefinition.category switch
        {
            ItemCategory.Consumable => new ConsumableItem(itemDefinition, count),
            ItemCategory.Material => new MaterialItem(itemDefinition, count),
            ItemCategory.ExpBook => new MaterialItem(itemDefinition, count),
            _ => new MaterialItem(itemDefinition, count),
        };
    }

    void ImportEquip(EquipItemSaveData equipData)
    {
        if (equipData == null)
        {
            return;
        }

        ItemDefinition itemDefinition = GameDatabase.ItemDatabase.GetItemByID(equipData.itemId);
        if (itemDefinition is not EquipDefinition equipDefinition)
        {
            Debug.LogWarning($"读取武器失败，找不到武器: {equipData.itemId}");
            return;
        }

        EquipItem equipItem = new EquipItem(
            equipDefinition,
            equipData.level,
            equipData.refinementLevel,
            equipData.exp,
            rank: equipData.rank);
        long instanceId = equipData.instanceId > 0 ? equipData.instanceId : nextInstanceId;
        equipItem.SetInstanceId(instanceId);
        nextInstanceId = Math.Max(nextInstanceId, instanceId + 1);
        items.Add(equipItem);
        itemsByInstanceId.Add(equipItem.InstanceId, equipItem);
    }
}

public enum InventoryChangeType
{
    Added,
    Removed,
    StackChanged,
    Reset,
}

public readonly struct InventoryChangedEvent
{
    public readonly InventoryChangeType Type;
    public readonly InventoryItem Item;
    public readonly ItemStack Stack;

    public InventoryChangedEvent(InventoryChangeType type, InventoryItem item, ItemStack stack = null)
    {
        Type = type;
        Item = item;
        Stack = stack;
    }
}
