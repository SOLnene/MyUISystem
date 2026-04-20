// Editor/FaceControllerEditor.cs
using UnityEditor;
using UnityEngine;


/// <summary>
/// 拓展 FaceController 的 Inspector，添加表情导出功能：
/// </summary>
[CustomEditor(typeof(FaceController))]
public class FaceControllerEditor : Editor
{
    private FaceController controller;
    private string presetName = "NewExpression";
    private string savePath = "Assets/GameData/ModelView/FacePreset/";

    private void OnEnable()
    {
        controller = (FaceController)target;
    }

    public override void OnInspectorGUI()
    {
        // 先绘制默认 Inspector（保留原有字段）
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("表情导出工具", EditorStyles.boldLabel);

        // 导出名称输入
        presetName = EditorGUILayout.TextField("预设名称", presetName);

        // 保存路径（可自定义）
        EditorGUILayout.LabelField("保存路径", savePath);
        if (GUILayout.Button("选择保存路径"))
        {
            string path = EditorUtility.SaveFolderPanel("选择表情预设保存路径", savePath, "");
            if (!string.IsNullOrEmpty(path))
            {
                savePath = path.Replace(Application.dataPath, "Assets") + "/";
            }
        }

        // 一键导出按钮
        if (GUILayout.Button("导出当前表情为 Preset"))
        {
            ExportCurrentExpression();
        }

        // 测试按钮（可选）
        if (GUILayout.Button("重置所有表情"))
        {
            controller.ResetAll();
        }
    }

    private void ExportCurrentExpression()
    {
        if (controller == null)
        {
            EditorUtility.DisplayDialog("错误", "未选中 FaceController", "确定");
            return;
        }

        var preset = ScriptableObject.CreateInstance<FaceExpressionPreset>();
        preset.name = presetName;
        preset.expressionName = presetName;
        preset.blends.Clear();

        var allRenderers = controller.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        // 遍历所有 Renderer，收集当前实际权重 > 0 的 BlendShape
        foreach (var renderer in allRenderers) // 假设你的 controller 有 faceRenderers 列表
        {
            var mesh = renderer.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0) continue;

            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                float weight = renderer.GetBlendShapeWeight(i);
                if (weight > 0.01f) // 过滤微小值，避免导出噪声
                {
                    string name = mesh.GetBlendShapeName(i);
                    float normalized = weight / 100f; // 转为 0~1

                    preset.blends.Add(new FaceExpressionPreset.BlendData
                    {
                        blendShapeName = name,
                        weight = normalized
                    });
                }
            }
        }
        
        if (preset.blends.Count == 0)
        {
            EditorUtility.DisplayDialog("导出失败", "当前没有非零表情，无法导出", "确定");
            return;
        }

        // 生成唯一路径并保存
        string fullPath = $"{savePath}{presetName}.asset";
        fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
        AssetDatabase.CreateAsset(preset, fullPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"表情预设导出成功: {fullPath}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = preset;

        EditorUtility.DisplayDialog("导出成功", $"保存到: {fullPath}", "确定");
    }
}