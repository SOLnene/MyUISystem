using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class ItemFactory
{
    static Dictionary<string, ItemDefinition> itemDatabase;

    /// <summary>
    /// 创建测试 InventoryItem 列表
    /// </summary>
    public static List<InventoryItem> CreateTestItems()
    {
        var items = new List<InventoryItem>();

        foreach (var def in GameDatabase.ItemDatabase.allItems)
        {
            switch (def.category)
            {
                case ItemCategory.Equip:
                    AddTestEquipItems(items, def as EquipDefinition);
                    break;
                case ItemCategory.Consumable:
                    items.Add(new ConsumableItem(def, count: Random.Range(1, 10)));
                    break;
                case ItemCategory.Material:
                    items.Add(new InventoryItem(def)); // 普通材料用基类
                    break;
                default:
                    items.Add(new InventoryItem(def));
                    break;
            }
        }

        return items;
    }

    static void AddTestEquipItems(List<InventoryItem> items, EquipDefinition def)
    {
        if (def == null) return;

        int copyCount = GetTestEquipCopyCount(def.stars);
        for (int i = 0; i < copyCount; i++)
        {
            items.Add(new EquipItem(def, level: Random.Range(1, 10), refine: Random.Range(1, 5)));
        }
    }

    static int GetTestEquipCopyCount(int stars)
    {
        if (stars <= 1) return 40;
        if (stars == 2) return 24;
        if (stars == 3) return 16;
        return 1;
    }

    public static List<InventoryItem> CreateAllFromDatabase()
    {
        var db = GameDatabase.ItemDatabase;
        if (db == null || db.allItems == null)
        {
            Debug.LogError("ItemDatabase 未加载或为空！");
            return null;
        }

        var items = new List<InventoryItem>();

        foreach (var def in db.allItems)
        {
            switch (def.category)
            {
                case ItemCategory.Equip:
                    var weaponDef = def as EquipDefinition;
                    if (weaponDef == null)
                    {
                        Debug.LogWarning($"Item {def.itemName} 声称是 Weapon 但不是 WeaponDefinition！");
                        continue;
                    }
                    items.Add(new EquipItem(weaponDef, level: Random.Range(1, 10), refine: Random.Range(1, 5)));
                    break;

                case ItemCategory.Consumable:
                    items.Add(new ConsumableItem(def, count: Random.Range(1, 10)));
                    break;

                case ItemCategory.Material:
                default:
                    items.Add(new InventoryItem(def));
                    break;
            }
        }

        return items;
    }

    /// <summary>
    /// 根据 Key 或 ID 创建单个物品
    /// </summary>
    public static InventoryItem CreateItem(string keyOrId,int needCount = 1)
    {
        var db = GameDatabase.ItemDatabase;
        if (db == null)
        {
            Debug.LogError("ItemDatabase 未加载！");
            return null;
        }

        ItemDefinition def = null;
        if (int.TryParse(keyOrId, out int id))
            def = db.GetItemByID(id);
        else
            def = db.GetItemByKey(keyOrId);

        if (def == null)
        {
            Debug.LogError($"找不到物品定义：{keyOrId}");
            return null;
        }

        switch (def.category)
        {
            case ItemCategory.Equip:
                return new EquipItem(def as EquipDefinition);
            case ItemCategory.Consumable:
                return new ConsumableItem(def, count: 1);
            case ItemCategory.Material:
                return new MaterialItem(def, count: 1,needCount);
            default:
                return new InventoryItem(def);
        }
    }

    
    public static EquipItem CreateWeaponItem()
    {
        return new EquipItem(GameDatabase.ItemDatabase.GetItemByKey("Claymore_Aniki") as EquipDefinition
            , level: Random.Range(1, 10), refine: Random.Range(1, 5));
    }

    /// <summary>
    /// 创建随机武器物品，等级和精炼均为1
    /// </summary>
    /// <returns></returns>
    public static EquipItem CreateRandomWeaponItem()
    {
        var weaponDefs = GameDatabase.ItemDatabase.allItems.FindAll(def => def.category == ItemCategory.Equip);
        if (weaponDefs.Count == 0)
        {
            Debug.LogError("ItemDatabase 中没有武器定义！");
            return null;
        }

        var randomDef = weaponDefs[Random.Range(0, weaponDefs.Count)] as EquipDefinition;
        return new EquipItem(randomDef, level: 1, refine: 1);
    }    
    
    //todo:未来有更多种类的ItemSlotView时，可以增加一个参数指定类型，或者直接传入预制体路径
    public static async UniTask<ItemSlotView> InstantiateItemSlot(ItemSlotViewModel viewModel, Transform parent,
        string prefabAddress = "ui/prefab/item_slot", bool active = true)
    {
        var prefab = await ResourceManager.Instance.InstantiateItemAsync(prefabAddress, parent, active);
        if (prefab == null)
        {
            Debug.LogError("加载 ItemSlot 预制体失败");
            return null;
        }
      
        //角色界面装备选择界面会因为对象池导致scale问题
        
        prefab.transform.localScale = Vector3.one;
        prefab.transform.localRotation = Quaternion.identity;
        
        var item = prefab.GetComponent<ItemSlotView>();
        return item;
    }
    
}
