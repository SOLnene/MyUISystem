using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


public class CharacterVisualDatabaseUpdaterWindow : EditorWindow
{
    private CharacterVisualDatabase database;
        private DefaultAsset folder; // 用于选择 ItemDefinition 文件夹
    
        [MenuItem("Tools/Database/CharacterVisual Database Updater")]
        public static void ShowWindow()
        {
            var window = GetWindow<CharacterVisualDatabaseUpdaterWindow>("CharacterVisual Database Updater");
            window.minSize = new Vector2(420, 220);
            window.Show();
        }
    
        private void OnGUI()
        {
            GUILayout.Label("📦 CharacterVisual Database 自动更新工具", EditorStyles.boldLabel);
            GUILayout.Space(10);
    
            database = (CharacterVisualDatabase)EditorGUILayout.ObjectField("CharacterVisual Database", database, typeof(CharacterVisualDatabase), false);
            folder = (DefaultAsset)EditorGUILayout.ObjectField("Item Folder", folder, typeof(DefaultAsset), false);
    
            GUILayout.Space(10);
    
            if (GUILayout.Button("🔍 扫描并更新", GUILayout.Height(30)))
            {
                if (database == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先指定一个 CharacterVisual 资源文件！", "确定");
                    return;
                }
    
                if (folder == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择包含 CharacterVisual 的文件夹！", "确定");
                    return;
                }
    
                string folderPath = AssetDatabase.GetAssetPath(folder);
                UpdateDatabase(database, folderPath);
            }
    
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("此工具会自动扫描指定文件夹下的所有 CharacterVisual 资源，并更新到数据库中。\n重复项会跳过。", MessageType.Info);
        }
    
        private void UpdateDatabase(CharacterVisualDatabase database, string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterVisualDefinition", new[] { folderPath });

            var existingKeys = new HashSet<string>();
    
            foreach (var v in database.AllVisuals)
            {
                if (v != null)
                    existingKeys.Add(v.characterKey);
            }
    
            int added = 0, skipped = 0;
    
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDefinition>(path);
                if (visual == null) continue;
    
                if (!existingKeys.Contains(visual.characterKey))
                {
                    database.Add(visual);
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
    
            Debug.Log($"✅ CharacterVisual Database 更新完成！新增 {added} 项，跳过 {skipped} 项。");
        }
}
