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

    [MenuItem("Tools/Generator/生成 ItemDefinitions (自动识别类型)")]
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
            var generator = new ItemDefinitionGenerator(
                overwriteExisting,
                markAddressable,
                addressPrefix,
                addressLabel
                );
            var result = generator.Generate(spriteFolder,outputFolder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "完成",
                $"新建：{result.createdCount}\n" +
                $"覆盖：{result.replacedCount}\n" +
                $"跳过：{result.skippedCount}",
                "确定"
                );
        }
        
    }
}

//todo:挪出去
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

