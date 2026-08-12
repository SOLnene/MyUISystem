using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class ItemDatabaseUpdaterWindow : EditorWindow
{
    private ItemDatabase database;
    private DefaultAsset folder; // 用于选择 ItemDefinition 文件夹

    [MenuItem("Tools/Database/Item Database Updater")]
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

        database = (ItemDatabase)EditorGUILayout.ObjectField("Item Database", database, typeof(ItemDatabase), false);
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

    private void UpdateDatabase(ItemDatabase database, string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { folderPath });

        var existingItems = new Dictionary<int, ItemDefinition>();
        database.allItems.RemoveAll(i => i == null); // 清理空引用

        foreach (var item in database.allItems)
        {
            if (item != null)
                existingItems[item.id] = item;
        }

        int added = 0, skipped = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null) continue;

            if (!existingItems.ContainsKey(item.id))
            {
                database.allItems.Add(item);
                added++;
            }
            else
            {
                skipped++;
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "更新完成",
            $"扫描路径：{folderPath}\n\n新增：{added} 项\n已存在：{skipped} 项\n\n数据库已更新！",
            "确定"
        );

        Debug.Log($"✅ Item Database 更新完成！新增 {added} 项，跳过 {skipped} 项。");
    }
}