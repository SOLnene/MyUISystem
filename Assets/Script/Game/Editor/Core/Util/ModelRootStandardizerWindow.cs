using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal sealed class ModelRootStandardizerWindow : EditorWindow
{
    GameObject sourceModel;
    string sourceNode;
    string report;
    Vector2 scroll;


    [MenuItem("Tools/Character Animation/模型根节点标准化")]
    static void Open()
    {
        var window = GetWindow<ModelRootStandardizerWindow>("模型根节点标准化");
        window.minSize = new Vector2(520, 320);
        window.UseSelection();
    }

    void UseSelection()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        FindCandidate();
    }

    void FindCandidate()
    {
        sourceNode = "";
        report = "";
        if (sourceModel == null) return;
        var candidates = new List<string>();
        foreach (Transform child in sourceModel.transform)
            if (child.Find("Bip001") != null) candidates.Add(child.name);
        if (candidates.Count == 1) sourceNode = candidates[0];
        else report = "无法自动确定唯一中间根，请从下拉列表明确选择。";
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("生成独立 FBX 副本，将指定直接子节点统一命名为 ModelRoot。" +
            "保留参考姿态和映射；副本以后重新导入时继续应用规则。不会运行 HoyoToon Full Setup。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        sourceModel = (GameObject)EditorGUILayout.ObjectField("源 FBX", sourceModel, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck()) FindCandidate();
        if (GUILayout.Button("使用当前选择")) UseSelection();

        if (sourceModel != null)
        {
            var names = new List<string> { "请选择节点" };
            foreach (Transform child in sourceModel.transform) names.Add(child.name);
            int index = Mathf.Max(0, names.IndexOf(sourceNode));
            index = EditorGUILayout.Popup("待改名直接子节点", index, names.ToArray());
            sourceNode = index == 0 ? "" : names[index];
        }
        EditorGUILayout.LabelField("转换后路径", "ModelRoot/Bip001/...");
        EditorGUILayout.LabelField("输出", "保存在源文件旁，自动避免覆盖同名资产");
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || sourceModel == null || string.IsNullOrEmpty(sourceNode)))
        {
            if (GUILayout.Button("检查配置")) Run(false);
            if (GUILayout.Button("生成副本并重新导入", GUILayout.Height(30))) Run(true);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(report ?? "", EditorStyles.wordWrappedLabel,
            GUILayout.MinHeight(160), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void Run(bool generate)
    {
        string output = null;
        try
        {
            string path = AssetDatabase.GetAssetPath(sourceModel);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择 Assets 内的 FBX 资产，而不是场景对象或 Prefab。");
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("找不到模型导入器。");
            if (ModelRootStandardizerPostprocessor.ReadSettings(importer) != null)
                throw new InvalidOperationException("此模型已标准化，请选择原始模型。");
            if (importer.optimizeGameObjects)
                throw new InvalidOperationException("初版要求源模型未开启 Optimize Game Objects，以便准确检查层级和引用。");
            if (importer.clipAnimations.Length > 0 || importer.defaultClipAnimations.Length > 0)
                throw new InvalidOperationException("初版只处理不含导入动画分段的模型；内嵌动画与 Mask 需另行适配。");
            Transform inner = sourceModel.transform.Find(sourceNode);
            if (inner == null || inner.parent != sourceModel.transform ||
                inner.Find("Bip001") == null || sourceNode == ModelRootStandardizerPostprocessor.RootName)
                throw new InvalidOperationException("请选择直接包含 Bip001 的内层模型根。");
            if (sourceModel.transform.Find(ModelRootStandardizerPostprocessor.RootName) != null)
                throw new InvalidOperationException("目标层级已经存在 ModelRoot。");
            int matchingNames = 0;
            foreach (Transform node in sourceModel.GetComponentsInChildren<Transform>(true))
                if (node != sourceModel.transform && node.name == sourceNode) matchingNames++;
            if (matchingNames != 1)
                throw new InvalidOperationException("模型后代存在重名包装节点，无法安全同步骨架描述。");
            Avatar sourceAvatar = FindAvatar(path);
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
                throw new InvalidOperationException("源模型没有有效的 Humanoid Avatar。");

            ModelRootStandardizerPostprocessor.SynchronizeDescription(importer, sourceNode, false);
            if (!generate)
            {
                report = "检查通过。将只修改内层节点名称、对应骨架名称引用和暴露路径。\n" +
                    "不会改变节点位置、旋转、缩放或 Human 映射。实际播放效果仍需验证。";
                return;
            }

            output = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, null) + "_ModelRoot.fbx");
            if (!AssetDatabase.CopyAsset(path, output))
                throw new InvalidOperationException("无法复制模型资产。");
            var copyImporter = (ModelImporter)AssetImporter.GetAtPath(output);
            // 复制后先比较参考姿态，防止以默认自动映射替代用户已经调好的导入配置。
            if (JsonUtility.ToJson(importer.humanDescription) != JsonUtility.ToJson(copyImporter.humanDescription))
                throw new InvalidOperationException("副本未完整保留 HumanDescription，已停止处理。");
            ModelRootStandardizerPostprocessor.Enable(copyImporter, sourceNode);
            copyImporter.SaveAndReimport();

            GameObject result = AssetDatabase.LoadAssetAtPath<GameObject>(output);
            Transform resultInner = result == null ? null : result.transform.Find(ModelRootStandardizerPostprocessor.RootName);
            Avatar avatar = FindAvatar(output);
            if (resultInner == null || avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("导入结果未通过 ModelRoot 层级或 Humanoid Avatar 检查。");
            VerifyHierarchy(sourceModel.transform, result.transform, sourceNode);
            report = $"已生成：{output}\n层级、节点局部变换与有效 Avatar 检查通过。\n" +
                "请使用这个 FBX 自带的 Avatar 播放转换后的动画；有效 Avatar 不代表视觉效果已经验证。";
            EditorGUIUtility.PingObject(result);
            Debug.Log("[模型根节点标准化] " + report);
        }
        catch (Exception exception)
        {
            report = exception.Message + (output == null ? "" : $"\n副本路径：{output}\n若副本已创建，将保留供检查，源资产未修改。");
            Debug.LogError("[模型根节点标准化] " + report);
        }
    }

    internal static Avatar FindAvatar(string path)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Avatar avatar) return avatar;
        return null;
    }

    static void VerifyHierarchy(Transform source, Transform result, string oldName)
    {
        Transform[] sourceNodes = source.GetComponentsInChildren<Transform>(true);
        if (sourceNodes.Length != result.GetComponentsInChildren<Transform>(true).Length)
            throw new InvalidOperationException("副本节点数量发生变化，不能视为仅改名成功。");
        foreach (Transform node in sourceNodes)
        {
            if (node == source) continue;
            string path = AnimationUtility.CalculateTransformPath(node, source);
            if (path == oldName || path.StartsWith(oldName + "/", StringComparison.Ordinal))
                path = ModelRootStandardizerPostprocessor.RootName + path.Substring(oldName.Length);
            Transform match = result.Find(path);
            if (match == null || Vector3.Distance(node.localPosition, match.localPosition) > 0.00001f ||
                Quaternion.Angle(node.localRotation, match.localRotation) > 0.001f ||
                Vector3.Distance(node.localScale, match.localScale) > 0.00001f)
                throw new InvalidOperationException($"节点缺失或局部变换发生变化：{path}");
        }
    }
}
