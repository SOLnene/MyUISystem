using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.IO;

public class AddressableWeaponRenamerWindow : EditorWindow
{
    private string folderPath = "Assets/AssetsPackage/Sprite/Item/Equip";
    private string labelName = "weapon_icon";
    private string prefix = ""; // 可选前缀
    private bool toLowerCase = true;
    private int modifiedCount = 0;

    [MenuItem("Tools/MyUISystem/资源/Addressables/批量命名图标", false, 200)]
    public static void ShowWindow()
    {
        GetWindow<AddressableWeaponRenamerWindow>("Weapon Icon Renamer");
    }

    private void OnGUI()
    {
        GUILayout.Label("🔧 批量修改武器图标 Addressable", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 文件夹路径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("图标文件夹路径:", GUILayout.Width(120));
        folderPath = EditorGUILayout.TextField(folderPath);
        if (GUILayout.Button("选择", GUILayout.Width(60)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择图标文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                {
                    folderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", "路径必须在Assets目录下！", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        labelName = EditorGUILayout.TextField("Label 名称:", labelName);
        prefix = EditorGUILayout.TextField("地址前缀 (可选):", prefix);
        toLowerCase = EditorGUILayout.Toggle("转为小写地址", toLowerCase);

        EditorGUILayout.Space();
        if (GUILayout.Button("🚀 执行批量修改", GUILayout.Height(35)))
        {
            BatchRenameWeaponIcons();
        }

        if (modifiedCount > 0)
        {
            EditorGUILayout.HelpBox($"已成功修改 {modifiedCount} 个 Addressable 资源。", MessageType.Info);
        }
    }

    private void BatchRenameWeaponIcons()
    {
        modifiedCount = 0;
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("错误", "没有找到 Addressable 设置！", "OK");
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            EditorUtility.DisplayDialog("错误", $"路径不存在:\n{folderPath}", "OK");
            return;
        }

        string[] assetPaths = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);

        foreach (var path in assetPaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                Debug.LogWarning($"未设置 Addressable: {path}");
                continue;
            }

            string fileName = Path.GetFileNameWithoutExtension(path); // UI_EquipIcon_Claymore_Aniki
            string address = prefix + fileName;
            if (toLowerCase) address = address.ToLower();

            entry.address = address;

            // 添加 Label
            if (!string.IsNullOrEmpty(labelName))
                entry.SetLabel(labelName, true, true);

            modifiedCount++;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"✅ 批量修改完成，共修改 {modifiedCount} 个图标。");

        EditorUtility.DisplayDialog("完成", $"已修改 {modifiedCount} 个 Addressable 图标。", "OK");
    }
}
