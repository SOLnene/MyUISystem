using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

internal sealed class AnimationBindingConverterWindow : EditorWindow
{
    const string OutputMarker = "CharacterAnimation.BindingSource.v1=";
    const string OutputDirectoryPrefsKey = "MyUISystem.AnimationBindingConverter.OutputDirectory";
    readonly List<AnimationClip> clips = new List<AnimationClip>();
    GameObject validationModel;
    DefaultAsset outputDirectoryAsset;
    string sourcePrefix = "";
    string outputDirectory = "";
    bool includeComponentCurves;
    bool excludeUpperArmTwistCurves;
    string report = "请选择 AnimationClip，然后预览转换。";
    Vector2 scroll;


    sealed class Plan
    {
        internal readonly List<EditorCurveBinding> from = new List<EditorCurveBinding>();
        internal readonly List<EditorCurveBinding> to = new List<EditorCurveBinding>();
        internal readonly List<bool> objectCurves = new List<bool>();
        internal readonly List<bool> removeOnly = new List<bool>();
        internal readonly StringBuilder report = new StringBuilder();
        internal bool excludesUpperArmTwist;
        internal int removedBindings;
        internal int errors;
    }

    [MenuItem("Tools/Character Animation/动画绑定路径转换")]
    static void Open()
    {
        var window = GetWindow<AnimationBindingConverterWindow>("动画绑定路径转换");
        window.minSize = new Vector2(580, 430);
        window.UseSelection();
    }

    void OnEnable()
    {
        outputDirectory = EditorPrefs.GetString(OutputDirectoryPrefsKey, "");
        if (!string.IsNullOrEmpty(outputDirectory) && !AssetDatabase.IsValidFolder(outputDirectory))
        {
            outputDirectory = "";
            EditorPrefs.DeleteKey(OutputDirectoryPrefsKey);
        }
        outputDirectoryAsset = string.IsNullOrEmpty(outputDirectory)
            ? null
            : AssetDatabase.LoadAssetAtPath<DefaultAsset>(outputDirectory);
    }

    void UseSelection()
    {
        clips.Clear();
        foreach (UnityEngine.Object selected in Selection.objects)
            if (selected is AnimationClip clip && !clips.Contains(clip)) clips.Add(clip);
        report = $"已选择 {clips.Count} 个动画。";
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("生成完整动画副本，统一模型内部绑定为 ModelRoot/。" +
            "原生 Animator 继续播放 Muscle 和附加曲线；不修改源动画。", MessageType.Info);
        if (GUILayout.Button("读取当前选中的 AnimationClip（支持多选）")) UseSelection();
        EditorGUILayout.LabelField("已选动画", clips.Count.ToString());
        validationModel = (GameObject)EditorGUILayout.ObjectField("验证模型（可选）", validationModel, typeof(GameObject), true);
        sourcePrefix = EditorGUILayout.TextField("待替换前缀（通常留空）", sourcePrefix);
        EditorGUILayout.LabelField("目标前缀", ModelRootStandardizerPostprocessor.RootName + "/");
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        DefaultAsset selectedOutputDirectory = (DefaultAsset)EditorGUILayout.ObjectField(
            "输出目录（留空使用源目录）", outputDirectoryAsset, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck()) SetOutputDirectory(selectedOutputDirectory);
        if (GUILayout.Button("选择", GUILayout.Width(52))) SelectOutputDirectory();
        if (GUILayout.Button("源目录", GUILayout.Width(60)))
            SetOutputDirectory(null);
        EditorGUILayout.EndHorizontal();
        includeComponentCurves = EditorGUILayout.Toggle("同时转换模型内其他组件曲线", includeComponentCurves);
        excludeUpperArmTwistCurves = EditorGUILayout.Toggle("排除 UpperArmTwist 曲线（诊断）", excludeUpperArmTwistCurves);
        using (new EditorGUI.DisabledScope(clips.Count == 0 || EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("预览路径变化与冲突")) Process(false);
            if (GUILayout.Button("生成 / 更新转换副本", GUILayout.Height(30))) Process(true);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        float height = Mathf.Max(220, EditorStyles.wordWrappedLabel.CalcHeight(
            new GUIContent(report), Mathf.Max(200, position.width - 40)));
        EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel, GUILayout.Height(height));
        EditorGUILayout.EndScrollView();
    }

    void SelectOutputDirectory()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string initialDirectory = string.IsNullOrEmpty(outputDirectory)
            ? Application.dataPath
            : Path.Combine(projectRoot, outputDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(initialDirectory)) initialDirectory = Application.dataPath;
        string selectedDirectory = EditorUtility.OpenFolderPanel("选择动画输出目录", initialDirectory, "");
        if (string.IsNullOrEmpty(selectedDirectory)) return;
        string projectRelativePath = FileUtil.GetProjectRelativePath(selectedDirectory).Replace('\\', '/');
        if (projectRelativePath != "Assets" && !projectRelativePath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            report = "输出目录必须位于当前项目的 Assets 文件夹内。";
            return;
        }
        SetOutputDirectory(AssetDatabase.LoadAssetAtPath<DefaultAsset>(projectRelativePath));
    }

    void SetOutputDirectory(DefaultAsset directoryAsset)
    {
        if (directoryAsset == null)
        {
            outputDirectoryAsset = null;
            outputDirectory = "";
            EditorPrefs.DeleteKey(OutputDirectoryPrefsKey);
            return;
        }
        string path = AssetDatabase.GetAssetPath(directoryAsset).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(path))
        {
            report = "请拖入 Project 窗口中的文件夹，不能使用普通文件。";
            return;
        }
        outputDirectoryAsset = directoryAsset;
        outputDirectory = path;
        EditorPrefs.SetString(OutputDirectoryPrefsKey, outputDirectory);
    }

    void Process(bool generate)
    {
        var text = new StringBuilder();
        string prefix = sourcePrefix.Trim().Trim('/');
        if (prefix.Contains("\\") || prefix.Contains("//"))
        {
            report = "前缀必须是使用 / 分隔的相对路径。";
            return;
        }
        foreach (AnimationClip source in clips)
        {
            if (source == null) continue;
            text.AppendLine($"【{source.name}】");
            try
            {
                Plan plan = BuildPlan(source, prefix, includeComponentCurves, excludeUpperArmTwistCurves, validationModel);
                text.Append(plan.report);
                if (generate)
                {
                    if (plan.errors > 0) text.AppendLine("存在阻断问题，未生成此动画。");
                    else if (plan.from.Count == 0) text.AppendLine("没有需要转换的绑定，未生成副本。");
                    else text.AppendLine("已保存：" + SaveConvertedClip(source, plan, outputDirectory));
                }
            }
            catch (Exception exception)
            {
                text.AppendLine("失败：" + exception.Message);
            }
            text.AppendLine();
        }
        report = text.ToString();
        if (generate) Debug.Log("[动画绑定路径转换]\n" + report);
    }

    static Plan BuildPlan(AnimationClip source, string prefix, bool includeComponents,
        bool excludeUpperArmTwist, GameObject model)
    {
        var plan = new Plan { excludesUpperArmTwist = excludeUpperArmTwist };
        var targets = new HashSet<EditorCurveBinding>();
        var messages = new HashSet<string>();
        var humanNames = new HashSet<string>();
        if (model != null)
        {
            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                plan.errors++;
                plan.report.AppendLine("阻断：验证模型必须是 Animator 所在根对象。");
            }
            else if (animator.avatar != null && animator.avatar.isHuman)
                foreach (HumanBone bone in animator.avatar.humanDescription.human) humanNames.Add(bone.boneName);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            AddBinding(binding, false);
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            AddBinding(binding, true);
        plan.report.AppendLine($"需处理 {plan.from.Count} 条绑定，其中移除 {plan.removedBindings} 条；阻断问题 {plan.errors} 项。");
        return plan;

        void AddBinding(EditorCurveBinding binding, bool objectCurve)
        {
            // 诊断副本只移除问题节点的 Transform 绑定，其余曲线继续沿用完整克隆的数据。
            if (excludeUpperArmTwist && binding.type == typeof(Transform) && IsUpperArmTwistPath(binding.path))
            {
                plan.from.Add(binding);
                plan.to.Add(default);
                plan.objectCurves.Add(objectCurve);
                plan.removeOnly.Add(true);
                plan.removedBindings++;
                Message($"排除节点 Transform 曲线：{binding.path}");
                return;
            }

            EditorCurveBinding target = binding;
            bool candidate = binding.type == typeof(Transform) ||
                (includeComponents && binding.type != typeof(Animator));
            if (binding.type == typeof(Animator)) candidate = false;

            if (candidate)
            {
                string path = binding.path;
                if (string.IsNullOrEmpty(path))
                {
                    plan.errors++;
                    Message("阻断：存在空路径的非 Animator 曲线，不能确定它属于外层还是内层根。");
                }
                else if (HasUnresolvedSegment(path))
                    Message($"警告：未解析路径保持原样，转换不能修复它：{path}");
                else if (path == ModelRootStandardizerPostprocessor.RootName ||
                    path.StartsWith(ModelRootStandardizerPostprocessor.RootName + "/", StringComparison.Ordinal))
                {
                    // 同一资产可重复运行；已经标准化的绑定不再增加一层。
                }
                else if (prefix.Length == 0)
                    target.path = ModelRootStandardizerPostprocessor.RootName + "/" + path;
                else if (path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal))
                    target.path = ModelRootStandardizerPostprocessor.RootName + path.Substring(prefix.Length);
                else Message($"警告：路径不匹配指定前缀，保持原样：{path}");
            }
            else if (binding.type != typeof(Animator))
                Message($"警告：未转换组件曲线 {binding.type?.Name}：{binding.path}/{binding.propertyName}");

            if (!targets.Add(target))
            {
                plan.errors++;
                Message($"阻断：多个绑定转换到同一目标：{target.path}/{target.propertyName}");
            }
            if (target.path != binding.path)
            {
                plan.from.Add(binding);
                plan.to.Add(target);
                plan.objectCurves.Add(objectCurve);
                plan.removeOnly.Add(false);
                Message($"{binding.path} → {target.path}");
            }
            if (model != null && target.type != typeof(Animator) && !string.IsNullOrEmpty(target.path))
            {
                Transform node = model.transform.Find(target.path);
                if (node == null) Message($"缺失节点：{target.path}");
                else if (target.type == typeof(Transform) && humanNames.Contains(node.name))
                    Message($"控制冲突：{target.path} 已属于 Humanoid 映射；本工具不修改映射或删除曲线。");
                else if (target.type != typeof(Transform) && node.GetComponent(target.type) == null)
                    Message($"缺失组件：{target.path} / {target.type.Name}");
            }
        }

        void Message(string message)
        {
            if (messages.Add(message)) plan.report.AppendLine(message);
        }
    }

    static bool HasUnresolvedSegment(string path)
    {
        foreach (string segment in path.Split('/'))
            if (segment.StartsWith("path_", StringComparison.Ordinal)) return true;
        return false;
    }

    static bool IsUpperArmTwistPath(string path)
    {
        int separator = path.LastIndexOf('/');
        string nodeName = separator >= 0 ? path.Substring(separator + 1) : path;
        return nodeName == "+UpperArmTwist L A01" || nodeName == "+UpperArmTwist L A02" ||
            nodeName == "+UpperArmTwist R A01" || nodeName == "+UpperArmTwist R A02";
    }

    static string SaveConvertedClip(AnimationClip source, Plan plan, string selectedOutputDirectory)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
            throw new InvalidOperationException("初版要求源动画位于 Assets 内，以便在原目录保存副本。");
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId))
            throw new InvalidOperationException("无法获取源动画标识。");
        string marker = OutputMarker + guid + ":" + localId +
            (plan.excludesUpperArmTwist ? ";NoArmTwist" : string.Empty);
        string sourceDirectory = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
        string directory = string.IsNullOrEmpty(selectedOutputDirectory) ? sourceDirectory : selectedOutputDirectory;
        if (!AssetDatabase.IsValidFolder(directory))
            throw new InvalidOperationException("输出目录不存在或不在 Assets 内。");
        string output = null;
        // 使用 GUID + localID 识别同一 FBX 的不同子 Clip；仅更新本工具明确归属的输出。
        foreach (string assetGuid in AssetDatabase.FindAssets("t:AnimationClip", new[] { directory }))
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuid);
            if (Path.GetDirectoryName(path).Replace('\\', '/') != directory ||
                !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) || path == sourcePath) continue;
            if (AssetImporter.GetAtPath(path).userData == marker)
            {
                if (output != null) throw new InvalidOperationException("找到多个同源转换输出，请先明确保留哪一个。");
                output = path;
            }
        }
        string safeName = source.name;
        foreach (char invalid in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
        string outputSuffix = plan.excludesUpperArmTwist
            ? (safeName.EndsWith("_ModelRoot", StringComparison.Ordinal) ? "_NoArmTwist.anim" : "_ModelRoot_NoArmTwist.anim")
            : "_ModelRoot.anim";
        output ??= AssetDatabase.GenerateUniqueAssetPath(directory + "/" + safeName + outputSuffix);

        AnimationClip copy = Instantiate(source);
        try
        {
            copy.name = Path.GetFileNameWithoutExtension(output);
            copy.hideFlags = HideFlags.None;
            // 完整克隆保留 Muscle、事件与剪辑设置；先删除所有旧绑定，再添加新绑定。
            for (int i = 0; i < plan.from.Count; i++)
            {
                if (plan.objectCurves[i]) AnimationUtility.SetObjectReferenceCurve(copy, plan.from[i], null);
                else AnimationUtility.SetEditorCurve(copy, plan.from[i], null);
            }
            for (int i = 0; i < plan.from.Count; i++)
            {
                if (plan.removeOnly[i]) continue;
                if (plan.objectCurves[i]) AnimationUtility.SetObjectReferenceCurve(copy, plan.to[i],
                    AnimationUtility.GetObjectReferenceCurve(source, plan.from[i]));
                else AnimationUtility.SetEditorCurve(copy, plan.to[i], AnimationUtility.GetEditorCurve(source, plan.from[i]));
            }
            // Unity 编辑曲线时会重算 stopTime；恢复源剪辑边界，避免转换改变播放时序。
            AnimationUtility.SetAnimationClipSettings(copy, AnimationUtility.GetAnimationClipSettings(source));
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
            if (existing != null)
            {
                Undo.RecordObject(existing, "更新标准路径动画");
                EditorUtility.CopySerialized(copy, existing);
                // 临时 Clip 的运行时绑定缓存尚未经过资产导入；用公开曲线接口重写一次，强制 Unity 重建绑定表。
                EditorCurveBinding[] floatBindings = AnimationUtility.GetCurveBindings(existing);
                var floatCurves = new AnimationCurve[floatBindings.Length];
                for (int i = 0; i < floatBindings.Length; i++)
                {
                    floatCurves[i] = AnimationUtility.GetEditorCurve(existing, floatBindings[i]);
                    AnimationUtility.SetEditorCurve(existing, floatBindings[i], null);
                }
                EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(existing);
                var objectCurves = new ObjectReferenceKeyframe[objectBindings.Length][];
                for (int i = 0; i < objectBindings.Length; i++)
                {
                    objectCurves[i] = AnimationUtility.GetObjectReferenceCurve(existing, objectBindings[i]);
                    AnimationUtility.SetObjectReferenceCurve(existing, objectBindings[i], null);
                }
                AnimationUtility.SetEditorCurves(existing, floatBindings, floatCurves);
                AnimationUtility.SetObjectReferenceCurves(existing, objectBindings, objectCurves);
                AnimationUtility.SetAnimationClipSettings(existing, AnimationUtility.GetAnimationClipSettings(copy));
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(copy, output);
                copy = null;
            }
            AssetDatabase.SaveAssets();
            AssetImporter importer = AssetImporter.GetAtPath(output);
            importer.userData = marker;
            AssetDatabase.WriteImportSettingsIfDirty(output);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AnimationClip>(output));
            return output;
        }
        finally
        {
            if (copy != null) DestroyImmediate(copy);
        }
    }

}
