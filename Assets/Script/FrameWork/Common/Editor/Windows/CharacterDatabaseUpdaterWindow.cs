using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CharacterDatabaseUpdaterWindow : EditorWindow
{
    private CharacterDatabase database;
        private DefaultAsset folder; // 用于选择 ItemDefinition 文件夹
    
        [MenuItem("Tools/Database/Character Database Updater")]
        public static void ShowWindow()
        {
            var window = GetWindow<CharacterDatabaseUpdaterWindow>("Character Database Updater");
            window.minSize = new Vector2(420, 220);
            window.Show();
        }
    
        private void OnGUI()
        {
            GUILayout.Label("📦 Character Database 自动更新工具", EditorStyles.boldLabel);
            GUILayout.Space(10);
    
            database = (CharacterDatabase)EditorGUILayout.ObjectField("Character Database", database, typeof(CharacterDatabase), false);
            folder = (DefaultAsset)EditorGUILayout.ObjectField("Character Folder", folder, typeof(DefaultAsset), false);
    
            GUILayout.Space(10);
    
            if (GUILayout.Button("🔍 扫描并更新", GUILayout.Height(30)))
            {
                if (database == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先指定一个 Character 资源文件！", "确定");
                    return;
                }
    
                if (folder == null)
                {
                    EditorUtility.DisplayDialog("错误", "请先选择包含 Character 的文件夹！", "确定");
                    return;
                }
    
                string folderPath = AssetDatabase.GetAssetPath(folder);
                UpdateDatabase(database, folderPath);
            }
    
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("此工具会自动扫描指定文件夹下的所有 Character 资源，并更新到数据库中。\n重复项会跳过。", MessageType.Info);
        }
    
        private void UpdateDatabase(CharacterDatabase database, string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition", new[] { folderPath });

            var existingKeys = new HashSet<string>();
    
            foreach (var v in database.AllCharacter)
            {
                if (v != null)
                    existingKeys.Add(v.key);
            }
    
            int added = 0, skipped = 0;
    
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                if (definition == null) continue;
    
                if (!existingKeys.Contains(definition.key))
                {
                    database.Add(definition);
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
    
            Debug.Log($"✅ Character Database 更新完成！新增 {added} 项，跳过 {skipped} 项。");
        }
}
