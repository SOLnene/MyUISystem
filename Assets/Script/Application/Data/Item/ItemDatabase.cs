using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> allItems;

    Dictionary<int, ItemDefinition> dict;

    public ItemDefinition GetItemByID(int id)
    {
        EnsureDictBuilt();
        return dict.TryGetValue(id, out var item) ? item : null;
    }

    public ItemDefinition GetItemByKey(string key)
    {
        EnsureDictBuilt();
        foreach (var kvp in dict)
        {
            if (kvp.Value.key == key)
                return kvp.Value;
        }
        return null;
    }
    
    Dictionary<int, ItemDefinition> BuildDict()
    {
        var dict = new Dictionary<int, ItemDefinition>();
        foreach (var item in allItems)
        {
            dict[item.id] = item;
        }
        return dict;
    }
    
    private void EnsureDictBuilt()
    {
        if (dict != null) return;

        dict = new Dictionary<int, ItemDefinition>();
        foreach (var def in allItems)
        {
            if (def == null)
            {
                Debug.LogWarning("ItemDatabase 中存在空引用 ItemDefinition！");
                continue;
            }

            if (dict.ContainsKey(def.id))
            {
                Debug.LogWarning($"重复的 ItemDefinition ID: {def.id} ({def.itemName})");
                continue;
            }

            dict.Add(def.id, def);
        }
    }
}
