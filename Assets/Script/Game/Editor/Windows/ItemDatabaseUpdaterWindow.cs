using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class ItemDatabaseUpdaterWindow : EditorWindow
{
    private TextAsset database;
    private DefaultAsset folder; // 用于选择 ItemDefinition 文件夹

    [MenuItem("Tools/MyUISystem/数据生成/道具/更新 ItemDatabase", false, 101)]
    public static void ShowWindow()
    {
        var window = GetWindow<ItemDatabaseUpdaterWindow>("Item Database Updater");
        window.minSize = new Vector2(420, 220);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("📦 Item Database 自动更新工具", EditorStyles.boldLabel);
        GUILayout.Space(10);

        database = (TextAsset)EditorGUILayout.ObjectField("Item Database (JSON)", database, typeof(TextAsset), false);
        folder = (DefaultAsset)EditorGUILayout.ObjectField("Item Folder", folder, typeof(DefaultAsset), false);

        GUILayout.Space(10);

        if (GUILayout.Button("🔍 扫描并更新", GUILayout.Height(30)))
        {
            if (database == null)
            {
                EditorUtility.DisplayDialog("错误", "请先指定一个 ItemDatabase 资源文件！", "确定");
                return;
            }

            if (folder == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择包含 ItemDefinition 的文件夹！", "确定");
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(folder);
            UpdateDatabase(database, folderPath);
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("此工具会自动扫描指定文件夹下的所有 ItemDefinition 资源，并更新到数据库中。\n重复项会跳过。", MessageType.Info);
    }

    private void UpdateDatabase(TextAsset configAsset, string folderPath)
    {
        string configPath = AssetDatabase.GetAssetPath(configAsset);
        var database = ItemConfigJson.Parse(configAsset.text);
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { folderPath });

        var existingItems = new Dictionary<int, ItemDefinition>();
        database.allItems.RemoveAll(i => i == null); // 清理空引用

        foreach (var item in database.allItems)
        {
            if (item != null)
                existingItems[item.id] = item;
        }

        var existingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in database.allItems)
            existingKeys.Add(item.key);
        int added = 0, skipped = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == configPath || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            foreach (var item in ItemConfigJson.Parse(source.text).allItems)
            {
                if (existingItems.ContainsKey(item.id) || existingKeys.Contains(item.key))
                {
                    skipped++;
                    continue;
                }
                database.allItems.Add(item);
                existingItems.Add(item.id, item);
                existingKeys.Add(item.key);
                added++;
            }
        }

        File.WriteAllText(configPath, ItemConfigJson.Serialize(database.allItems), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);

        EditorUtility.DisplayDialog(
            "更新完成",
            $"扫描路径：{folderPath}\n\n新增：{added} 项\n已存在：{skipped} 项\n\n数据库已更新！",
            "确定"
        );

        Debug.Log($"✅ Item Database 更新完成！新增 {added} 项，跳过 {skipped} 项。");
    }
}
