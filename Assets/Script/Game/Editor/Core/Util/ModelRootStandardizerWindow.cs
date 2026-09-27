using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal sealed class ModelRootStandardizerWindow : EditorWindow
{
    GameObject sourceModel;
    string sourceNode;
    string report;
    Vector2 scroll;


    [MenuItem("Tools/MyUISystem/角色展示/模型/模型根节点标准化", false, 320)]
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
        foreach (Transform child in sourceModel.GetComponentsInChildren<Transform>(true))
            if (child != sourceModel.transform && child.Find("Bip001") != null)
                candidates.Add(AnimationUtility.CalculateTransformPath(child, sourceModel.transform));
        if (candidates.Count == 1) sourceNode = candidates[0];
        else report = "无法自动确定唯一中间根，请从下拉列表明确选择。";
    }

    void OnGUI()
    {
        if (sourceNode != null && sourceNode.Contains("/"))
            EditorGUILayout.HelpBox("生成独立 FBX 副本，将多层纯包装节点合并为 xxx/ModelRoot；" +
                "同步参考姿态和暴露路径，实际骨骼与网格的局部变换保持不变。", MessageType.Info);
        else
            EditorGUILayout.HelpBox("生成独立 FBX 副本，将指定直接子节点统一命名为 ModelRoot。" +
            "保留参考姿态和映射；副本以后重新导入时继续应用规则。不会运行 HoyoToon Full Setup。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        sourceModel = (GameObject)EditorGUILayout.ObjectField("源 FBX", sourceModel, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck()) FindCandidate();
        if (GUILayout.Button("使用当前选择")) UseSelection();

        if (sourceModel != null)
        {
            var names = new List<string> { "请选择节点" };
            foreach (Transform child in sourceModel.GetComponentsInChildren<Transform>(true))
                if (child != sourceModel.transform && child.Find("Bip001") != null)
                    names.Add(AnimationUtility.CalculateTransformPath(child, sourceModel.transform));
            int index = Mathf.Max(0, names.IndexOf(sourceNode));
            index = EditorGUILayout.Popup(sourceNode != null && sourceNode.Contains("/")
                ? "待合并模型根（完整路径）" : "待改名直接子节点", index, names.ToArray());
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
            if (inner == null ||
                inner.Find("Bip001") == null || sourceNode == ModelRootStandardizerPostprocessor.RootName)
                throw new InvalidOperationException("请选择直接包含 Bip001 的内层模型根。");
            if (sourceModel.transform.Find(ModelRootStandardizerPostprocessor.RootName) != null)
                throw new InvalidOperationException("目标层级已经存在 ModelRoot。");
            int matchingNames = 0;
            foreach (Transform node in sourceModel.GetComponentsInChildren<Transform>(true))
                if (node != sourceModel.transform && node.name == sourceNode) matchingNames++;
            if (sourceNode.Contains("/"))
                ModelRootStandardizerPostprocessor.ValidateWrapperChain(sourceModel.transform, sourceNode);
            else if (matchingNames != 1)
                throw new InvalidOperationException("模型后代存在重名包装节点，无法安全同步骨架描述。");
            Avatar sourceAvatar = FindAvatar(path);
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
                throw new InvalidOperationException("源模型没有有效的 Humanoid Avatar。");

            ModelRootStandardizerPostprocessor.SynchronizeDescription(importer, sourceNode, false);
            if (!generate)
            {
                if (sourceNode.Contains("/"))
                    report = "检查通过。将合并包装层为 xxx/ModelRoot，并同步参考姿态；实际骨骼的局部变换保持不变。";
                else
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
            EnsureValidTPose(output);

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

    internal static GameObject GetOrCreateStandardizedModel(GameObject sourceModel, out string result)
    {
        if (sourceModel == null)
            throw new InvalidOperationException("请选择源模型。");

        Transform existingRoot = sourceModel.transform.Find(ModelRootStandardizerPostprocessor.RootName);
        if (existingRoot != null && existingRoot.Find("Bip001") != null)
        {
            result = "模型已包含标准根节点，直接使用：" + AssetDatabase.GetAssetPath(sourceModel);
            return sourceModel;
        }

        string path = AssetDatabase.GetAssetPath(sourceModel);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("自动生成 ModelRoot 只支持 Assets 内的 FBX 资产。");
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("找不到模型导入器。");
        if (ModelRootStandardizerPostprocessor.ReadSettings(importer) != null)
            throw new InvalidOperationException("源模型带有 ModelRoot 标准化标记，但导入层级中找不到 ModelRoot。");
        if (importer.optimizeGameObjects)
            throw new InvalidOperationException("自动生成 ModelRoot 要求源模型未开启 Optimize Game Objects。");
        if (importer.clipAnimations.Length > 0 || importer.defaultClipAnimations.Length > 0)
            throw new InvalidOperationException("自动生成 ModelRoot 暂不处理带内嵌动画分段的模型。");

        var candidates = new List<Transform>();
        foreach (Transform child in sourceModel.GetComponentsInChildren<Transform>(true))
            if (child != sourceModel.transform && child.Find("Bip001") != null) candidates.Add(child);
        if (candidates.Count != 1)
            throw new InvalidOperationException($"无法确定唯一的模型内部根，当前候选数：{candidates.Count}。");
        string sourceNode = AnimationUtility.CalculateTransformPath(candidates[0], sourceModel.transform);
        int matchingNames = 0;
        foreach (Transform node in sourceModel.GetComponentsInChildren<Transform>(true))
            if (node != sourceModel.transform && node.name == sourceNode) matchingNames++;
        if (sourceNode.Contains("/"))
            ModelRootStandardizerPostprocessor.ValidateWrapperChain(sourceModel.transform, sourceNode);
        else if (matchingNames != 1)
            throw new InvalidOperationException("模型后代存在重名包装节点，无法安全同步骨架描述。");

        Avatar sourceAvatar = FindAvatar(path);
        if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
            throw new InvalidOperationException("源模型没有有效的 Humanoid Avatar。");
        ModelRootStandardizerPostprocessor.SynchronizeDescription(importer, sourceNode, false);

        string output = Path.ChangeExtension(path, null) + "_ModelRoot.fbx";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(output);
        if (existing != null)
        {
            var existingImporter = AssetImporter.GetAtPath(output) as ModelImporter;
            ModelRootStandardizerPostprocessor.Settings settings = existingImporter == null
                ? null
                : ModelRootStandardizerPostprocessor.ReadSettings(existingImporter);
            Avatar existingAvatar = FindAvatar(output);
            if (settings == null || settings.sourceNode != sourceNode ||
                existing.transform.Find(ModelRootStandardizerPostprocessor.RootName) == null ||
                existingAvatar == null || !existingAvatar.isValid || !existingAvatar.isHuman)
                throw new InvalidOperationException("现有 ModelRoot 副本与当前源模型不匹配，请先检查该资产：" + output);
            EnsureValidTPose(output);
            existing = AssetDatabase.LoadAssetAtPath<GameObject>(output);
            VerifyHierarchy(sourceModel.transform, existing.transform, sourceNode);
            result = "已复用 ModelRoot 模型副本：" + output;
            return existing;
        }

        if (!AssetDatabase.CopyAsset(path, output))
            throw new InvalidOperationException("无法创建 ModelRoot 模型副本：" + output);
        var copyImporter = (ModelImporter)AssetImporter.GetAtPath(output);
        if (JsonUtility.ToJson(importer.humanDescription) != JsonUtility.ToJson(copyImporter.humanDescription))
            throw new InvalidOperationException("ModelRoot 副本未完整保留 HumanDescription，已停止处理。");
        ModelRootStandardizerPostprocessor.Enable(copyImporter, sourceNode);
        copyImporter.SaveAndReimport();
        EnsureValidTPose(output);

        GameObject standardized = AssetDatabase.LoadAssetAtPath<GameObject>(output);
        Avatar avatar = FindAvatar(output);
        if (standardized == null ||
            standardized.transform.Find(ModelRootStandardizerPostprocessor.RootName) == null ||
            avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("ModelRoot 模型副本未通过层级或 Humanoid Avatar 检查：" + output);
        VerifyHierarchy(sourceModel.transform, standardized.transform, sourceNode);
        result = "已生成 ModelRoot 模型副本：" + output;
        return standardized;
    }

    static void EnsureValidTPose(string modelPath)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (model == null || importer == null)
            throw new InvalidOperationException("无法加载待校正 T-Pose 的 ModelRoot 模型：" + modelPath);

        // Unity 未公开 Configure Avatar 的 Enforce T-Pose API；复用编辑器内部实现，并在调用前校验方法签名。
        Type setupTool = typeof(Editor).Assembly.GetType("UnityEditor.AvatarSetupTool");
        if (setupTool == null)
            throw new InvalidOperationException("当前 Unity 版本不提供 AvatarSetupTool，无法自动校正 T-Pose。");

        MethodInfo validateMethod = RequireMethod(setupTool, "IsPoseValidOnInstance", 2,
            typeof(GameObject));
        MethodInfo copyPoseMethod = RequireMethod(setupTool, "CopyPose", 2,
            typeof(GameObject));
        MethodInfo getModelBonesMethod = RequireMethod(setupTool, "GetModelBones", 3,
            typeof(Transform));
        MethodInfo getHumanBonesMethod = RequireMethod(setupTool, "GetHumanBones", 2,
            typeof(SerializedProperty));
        MethodInfo makePoseValidMethod = RequireMethod(setupTool, "MakePoseValid", 1, null);
        MethodInfo savePoseMethod = RequireMethod(setupTool, "TransferPoseToDescription", 2,
            typeof(SerializedProperty));

        GameObject instance = UnityEngine.Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            using var serialized = new SerializedObject(importer);
            SerializedProperty human = serialized.FindProperty("m_HumanDescription.m_Human");
            SerializedProperty skeleton = serialized.FindProperty("m_HumanDescription.m_Skeleton");
            if (human == null || skeleton == null)
                throw new InvalidOperationException("模型导入器缺少 Humanoid 映射或参考姿态数据。");

            // 先恢复模型资源的导入姿态，再从该姿态执行 T-Pose 校正，顺序与 Configure Avatar 一致。
            Invoke(copyPoseMethod, instance, model);
            Invoke(savePoseMethod, skeleton, instance.transform);
            object modelBones = Invoke(getModelBonesMethod, instance.transform, false, null);
            object humanBones = Invoke(getHumanBonesMethod, human, modelBones);
            Invoke(makePoseValidMethod, humanBones);
            Invoke(savePoseMethod, skeleton, instance.transform);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(importer);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }

        // 参考姿态写回 ModelImporter 后必须重导入，新的 Avatar 才会使用校正结果。
        importer.SaveAndReimport();
        model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (model == null || importer == null)
            throw new InvalidOperationException("T-Pose 校正后无法重新加载 ModelRoot 模型：" + modelPath);
        using var resultValidation = new SerializedObject(importer);
        if (!(bool)Invoke(validateMethod, model, resultValidation))
            throw new InvalidOperationException("Unity 自动校正后模型仍不是有效 T-Pose：" + modelPath);
    }

    static MethodInfo RequireMethod(Type type, string name, int parameterCount, Type firstParameterType)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == name && parameters.Length == parameterCount &&
                (firstParameterType == null || parameters[0].ParameterType == firstParameterType))
                return method;
        }
        throw new InvalidOperationException($"当前 Unity 版本的 AvatarSetupTool 缺少预期方法：{name}");
    }

    static object Invoke(MethodInfo method, params object[] arguments)
    {
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw new InvalidOperationException(
                $"Unity AvatarSetupTool.{method.Name} 执行失败。", exception.InnerException);
        }
    }

    static void VerifyHierarchy(Transform source, Transform result, string oldName)
    {
        Transform[] sourceNodes = source.GetComponentsInChildren<Transform>(true);
        Transform contentRoot = source.Find(oldName);
        int removedWrappers = oldName.Split('/').Length - 1;
        Transform modelRoot = result.Find(ModelRootStandardizerPostprocessor.RootName);
        if (result.childCount != 1 || modelRoot == null || modelRoot.Find("Bip001") == null)
            throw new InvalidOperationException("输出必须是唯一的 xxx/ModelRoot，且 ModelRoot 直接包含 Bip001。");
        if (sourceNodes.Length - removedWrappers != result.GetComponentsInChildren<Transform>(true).Length)
            throw new InvalidOperationException("副本节点数量发生变化，不能视为仅改名成功。");
        foreach (Transform node in sourceNodes)
        {
            if (node == source) continue;
            if (removedWrappers > 0 && node != contentRoot && contentRoot.IsChildOf(node)) continue;
            string path = AnimationUtility.CalculateTransformPath(node, source);
            if (path == oldName || path.StartsWith(oldName + "/", StringComparison.Ordinal))
                path = ModelRootStandardizerPostprocessor.RootName + path.Substring(oldName.Length);
            Transform match = result.Find(path);
            if (removedWrappers > 0 && node == contentRoot && match != null)
            {
                Matrix4x4 expected = source.worldToLocalMatrix * node.localToWorldMatrix;
                Matrix4x4 actual = result.worldToLocalMatrix * match.localToWorldMatrix;
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(expected[i] - actual[i]) > 0.00001f)
                        throw new InvalidOperationException("合并包装层后 ModelRoot 的变换与源模型不一致。");
                continue;
            }
            if (match == null || Vector3.Distance(node.localPosition, match.localPosition) > 0.00001f ||
                Quaternion.Angle(node.localRotation, match.localRotation) > 0.001f ||
                Vector3.Distance(node.localScale, match.localScale) > 0.00001f)
                throw new InvalidOperationException($"节点缺失或局部变换发生变化：{path}");
        }
    }
}
