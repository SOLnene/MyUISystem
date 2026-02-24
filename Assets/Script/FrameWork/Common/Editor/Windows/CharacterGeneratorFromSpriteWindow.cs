using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CharacterGeneratorFromSpriteWindow : EditorWindow
{
    private string spriteFolder = "Assets/AssetsPackage/UI/Sprite/Gacha/Chara";
    private string outputFolder = "Assets/GameData/Character/Definition";
    private bool overwriteExisting = false;
    private bool markAddressable = false;
    private string addressPrefix = "chara_"; // 可选前缀
    private string addressLabel = "character";
    private int generatedCount = 0;

    [MenuItem("Tools/Generator/生成 CharacterDefinition (自动识别类型)")]
    public static void ShowWindow()
    {
        GetWindow<CharacterGeneratorFromSpriteWindow>("CharacterDefinition生成器");
    }

    private void OnGUI()
    {
        GUILayout.Label("📦 从 Sprite 批量生成 CharacterDefinition（自动识别类型）", EditorStyles.boldLabel);
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
        if (GUILayout.Button("🚀 生成 CharacterDefinition", GUILayout.Height(36)))
        {
            var generator = new CharacterDefinitionGenerator(
                overwriteExisting,
                markAddressable,
                addressPrefix,
                addressLabel);
            
            var result = generator.Generate(spriteFolder, outputFolder);
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
