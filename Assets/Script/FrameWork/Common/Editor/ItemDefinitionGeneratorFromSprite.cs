// 文件: Assets/Editor/ItemDefinitionGeneratorFromSpriteWindow.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

/// <summary>
/// 从 Sprite 文件夹批量生成对应类型的 ItemDefinition（支持自动识别 WeaponDefinition）
/// 支持：
///  - 文件名示例：UI_EquipIcon_Sword_Arakalari 或 UI_EquipIcon_Sword_Darker_Awaken
///  - 自动合并 name 中多段（Arakalari / Darker_Awaken）
///  - 按 category 创建子文件夹（Weapon/Consumable/...）
///  - 可选：把 sprite 标记为 Addressable，address 使用统一规则，添加 label（如 weapon_icon）
/// 注意：ItemDefinition、WeaponDefinition 必须在运行时程序集（非 Editor 文件夹）中独立 .cs
/// </summary>
public class ItemDefinitionGeneratorFromSpriteWindow : EditorWindow
{
    private string spriteFolder = "Assets/AssetsPackage/Sprite/Item/Equip";
    private string outputFolder = "Assets/GameData/ItemDefinitions";
    private bool overwriteExisting = false;
    private bool markAddressable = false;
    private string addressPrefix = "icon_"; // 可选前缀
    private string addressLabel = "weapon_icon";
    private int generatedCount = 0;

    [MenuItem("Tools/Item/生成 ItemDefinitions (自动识别类型)")]
    public static void ShowWindow()
    {
        GetWindow<ItemDefinitionGeneratorFromSpriteWindow>("ItemDefinition生成器");
    }

    private void OnGUI()
    {
        GUILayout.Label("📦 从 Sprite 批量生成 ItemDefinition（自动识别类型）", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // sprite 文件夹选择
        EditorGUILayout.BeginHorizontal();
        spriteFolder = EditorGUILayout.TextField("Sprite 文件夹", spriteFolder);
        if (GUILayout.Button("选择", GUILayout.Width(80)))
        {
            string path = EditorUtility.OpenFolderPanel("选择 Sprite 文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                    spriteFolder = "Assets" + path.Substring(Application.dataPath.Length);
                else
                    EditorUtility.DisplayDialog("错误", "必须选择在 Assets 目录内的路径！", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();

        // 输出文件夹选择
        EditorGUILayout.BeginHorizontal();
        outputFolder = EditorGUILayout.TextField("输出文件夹", outputFolder);
        if (GUILayout.Button("选择", GUILayout.Width(80)))
        {
            string path = EditorUtility.OpenFolderPanel("选择输出文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                    outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                else
                    EditorUtility.DisplayDialog("错误", "必须选择在 Assets 目录内的路径！", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();

        overwriteExisting = EditorGUILayout.Toggle("覆盖已有文件", overwriteExisting);
        EditorGUILayout.Space();

        GUILayout.Label("Addressable 设置（可选）", EditorStyles.boldLabel);
        markAddressable = EditorGUILayout.Toggle("标记 Sprite 为 Addressable", markAddressable);
        addressPrefix = EditorGUILayout.TextField("Address 前缀", addressPrefix);
        addressLabel = EditorGUILayout.TextField("Address Label", addressLabel);

        EditorGUILayout.Space();
        if (GUILayout.Button("🚀 生成 ItemDefinition", GUILayout.Height(36)))
        {
            GenerateItemDefinitions();
        }

        if (generatedCount > 0)
        {
            EditorGUILayout.HelpBox($"✅ 已生成 {generatedCount} 个定义文件。", MessageType.Info);
        }
    }

    private void GenerateItemDefinitions()
    {
        generatedCount = 0;

        if (!Directory.Exists(spriteFolder))
        {
            EditorUtility.DisplayDialog("错误", $"Sprite 文件夹不存在：\n{spriteFolder}", "确定");
            return;
        }

        // 确保输出路径存在
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        // 初始化 Addressables 设置（如果需要）
        AddressableAssetSettings aaSettings = null;
        if (markAddressable)
        {
            aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (aaSettings == null)
            {
                if (!EditorUtility.DisplayDialog("Addressables 未配置", "找不到 Addressables 设置，是否继续生成（不操作 Addressables）？", "继续", "取消"))
                    return;
                markAddressable = false;
            }
        }

        // 搜索 png、jpg 等
        string[] spritePaths = Directory.GetFiles(spriteFolder, "*.*", SearchOption.AllDirectories)
                                       .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                                || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                                || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                                                )
                                                
                                       .ToArray();

        foreach (var path in spritePaths)
        {
            string f = Path.GetFileNameWithoutExtension(path);

            // 🔹 排除包含 _Awaken 的文件
            if (f.Contains("_Awaken", StringComparison.OrdinalIgnoreCase))
                continue;
            
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);
            // 支持 UI_EquipIcon_Sword_Darker_Awaken 这种多段名字
            string[] parts = fileName.Split('_');
            if (parts.Length < 3) continue; // 至少需要 UI_xxx_xxx

            // 规则：寻找包含 "Equip" 或 "EquipIcon"/"ItemIcon" 的命名约定
            // 我们把“类型”解析为 parts 中的第 2 或第 3 段，兼容多种命名方式
            // 尝试找到关键段（Weapon 类型关键字）
            string typeToken = FindTypeToken(parts); // 例如 "Sword"
            if (string.IsNullOrEmpty(typeToken)) typeToken = "Unknown";

            // 名称：把 typeToken 后面的所有段都合并为 name（支持多单词）
            int typeIndex = Array.FindIndex(parts, p => string.Equals(p, typeToken, StringComparison.OrdinalIgnoreCase));
            string name = "UnknownName";
            if (typeIndex >= 0 && typeIndex < parts.Length - 1)
            {
                // 合并剩余段作为 name
                name = string.Join("_", parts.Skip(typeIndex + 1));
            }
            else
            {
                // 兜底：使用最后一段
                name = parts.Last();
            }

            string assetName = $"{typeToken}_{name}";
            string categoryFolderName = ParseCategoryFolder(typeToken); // e.g. Weapon
            string fullOutputFolder = Path.Combine(outputFolder, categoryFolderName);
            if (!Directory.Exists(fullOutputFolder))
                Directory.CreateDirectory(fullOutputFolder);

            string assetPath = $"{fullOutputFolder}/{assetName}.asset";

            if (File.Exists(assetPath) && !overwriteExisting)
                continue;

            // 选择要创建的 Definition 类型
            Type defType = GetDefinitionType(typeToken);
            if (!typeof(ItemDefinition).IsAssignableFrom(defType))
            {
                Debug.LogWarning($"识别到的类型 {defType} 不是 ItemDefinition 子类，跳过：{fileName}");
                continue;
            }

            // 创建实例（安全）
            var so = ScriptableObject.CreateInstance(defType) as ItemDefinition;
            if (so == null)
            {
                Debug.LogError($"无法创建 ScriptableObject：{defType.Name}，请检查类是否在运行时程序集并单独文件。");
                continue;
            }

            // 填充通用字段
            so.id = GenerateStableId(assetName);
            so.key = assetName;
            so.itemName = name.Replace('_', ' ');
            // 我们把 iconPath 存成 addressKey 风格（统一小写 + 前缀），这样运行时用 Addressables.LoadAsync(addressKey)
            string addressKey = fileName.ToLower();
            so.iconPath = addressKey;
            so.category = ParseItemCategory(typeToken);
            so.itemRarity = (ItemRarity)UnityEngine.Random.Range(0, 5);
            so.stars = (int)so.itemRarity+1;
            so.desc = "自动生成的物品";

            // WeaponDefinition 特殊字段
            if (so is EquipDefinition wdef)
            {
                wdef.baseAttack = UnityEngine.Random.Range(30, 150);
                wdef.baseCritRate = (float)Math.Round(UnityEngine.Random.Range(0.05f, 0.2f), 3);
                wdef.baseCritDamage = (float)Math.Round(UnityEngine.Random.Range(0.5f, 1.0f), 3);
                // ✅【新增】自动生成突破 Rank 表
                wdef.rankInfos = RankInfoGenerator.Generate(
                    rarity: (int)so.itemRarity + 1, 
                    baseGoldCost: 1000, 
                    baseMat: $"mat_{typeToken.ToLower()}_"
                    );
            }

            // 保存 asset
            AssetDatabase.CreateAsset(so, assetPath);
            generatedCount++;

            // 可选：把 sprite 标记为 Addressable（设置 address = addressKey，label = addressLabel）
            if (markAddressable && aaSettings != null)
            {
                MarkAssetAddressable(path, addressKey, addressLabel, aaSettings);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", $"成功生成 {generatedCount} 个定义文件。", "确定");
    }

    // ---------- 辅助方法 ----------

    // 在命名片段中查找可能的类型 token（例如 Sword, Claymore, Bow, Consumable, Material）
    private string FindTypeToken(string[] parts)
    {
        // 可扩展的关键字列表（全部小写）
        string[] keywords = new[] { "sword", "claymore", "bow", "polearm", "catalyst", "weapon", "consumable", "potion", "material", "equip", "item" };

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].ToLower();
            if (keywords.Contains(p))
                return parts[i]; // 返回原始大小写片段（便于后续拼接）
        }

        // fallback: 尝试 parts[2]（传统命名）
        if (parts.Length >= 3) return parts[2];

        return null;
    }

    // 决定存放子文件夹名
    private string ParseCategoryFolder(string typeToken)
    {
        var cat = ParseItemCategory(typeToken);
        switch (cat)
        {
            case ItemCategory.Equip: return "Equip";
            case ItemCategory.Consumable: return "Consumable";
            case ItemCategory.Material: return "Material";
            case ItemCategory.QuestItem: return "QuestItem";
            default: return "Misc";
        }
    }

    private ItemCategory ParseItemCategory(string typeToken)
    {
        if (string.IsNullOrEmpty(typeToken)) return ItemCategory.All;
        string lower = typeToken.ToLower();
        if (lower == "sword" || lower == "claymore" || lower == "bow" || lower == "polearm" || lower == "catalyst" || lower == "weapon" || lower == "equip")
            return ItemCategory.Equip;
        if (lower.Contains("potion") || lower.Contains("consumable") || lower.Contains("food"))
            return ItemCategory.Consumable;
        if (lower.Contains("material") || lower.Contains("ore"))
            return ItemCategory.Material;
        return ItemCategory.All;
    }

    private Type GetDefinitionType(string typeToken)
    {
        if (string.IsNullOrEmpty(typeToken)) return typeof(ItemDefinition);
        string lower = typeToken.ToLower();
        if (lower == "sword" || lower == "claymore" || lower == "bow" || lower == "polearm" || lower == "catalyst" || lower == "weapon" || lower == "equip")
            return typeof(EquipDefinition);

        if (lower.Contains("potion") || lower.Contains("consumable"))
            return typeof(ItemDefinition); // 可换成 ConsumableDefinition 如果你实现了

        if (lower.Contains("material"))
            return typeof(ItemDefinition);

        return typeof(ItemDefinition);
    }

    // 生成相对稳定的 int id（快速方案：Abs(hash)）
    private int GenerateStableId(string key)
    {
        // 注意：GetHashCode 在不同平台/不同运行时可能有差异，若需要跨平台稳定性请维护映射表或使用自增 ID 文件。
        return Math.Abs(key.GetHashCode());
    }

    // 将某个 assetPath 的资源标记为 Addressable，并设置 address & label（Editor only）
    private void MarkAssetAddressable(string assetPath, string addressKey, string label, AddressableAssetSettings aaSettings)
    {
        // assetPath 需要是 "Assets/xxx/yyy.png"
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) return;

        var entry = aaSettings.FindAssetEntry(guid);
        if (entry == null)
        {
            // 如果 entry 不存在，尝试将其加入默认组
            var group = aaSettings.DefaultGroup;
            entry = aaSettings.CreateOrMoveEntry(guid, group, false, false);
        }

        if (entry != null)
        {
            entry.address = addressKey;
            if (!string.IsNullOrEmpty(label))
                entry.SetLabel(label, true, true); // 安全添加 label（不会重复）
        }
    }
}

/// <summary>
/// 自动生成突破 Rank 表
/// </summary>
public static class RankInfoGenerator
{
    public static List<RankInfo> Generate(int rarity, int baseGoldCost = 1000, string baseMat = "mat_weapon_")
    {
        var ranks = new List<RankInfo>();

        int totalRanks = rarity switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            4 => 5,
            5 => 6,
            _ => 4
        };

        for (int i = 0; i < totalRanks; i++)
        {
            int currentRank = i;
            int maxLevel = (i + 1) * 20;

            var rank = new RankInfo
            {
                rank = currentRank,
                maxLevel = maxLevel,
                attackAddFlat = rarity * (i + 1) * 5,
                attackAddPercent = 0.02f * (i + 1),
                requirements = new List<BreakthroughRequirement>()
            };

            rank.requirements.Add(new BreakthroughRequirement($"{baseMat}{Mathf.Clamp(i + 1, 1, 5)}", (i + 1) * rarity * 2));

            // ✅【新增】自动金币需求
            rank.requirements.Add(new BreakthroughRequirement("gold", baseGoldCost * (i + 1) * rarity));

            ranks.Add(rank);
        }

        return ranks;
    }
}
