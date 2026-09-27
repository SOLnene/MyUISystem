using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal sealed class AuxiliaryTransformClipExtractorWindow : EditorWindow
{
    const string OutputDirectoryPrefsKey = "MyUISystem.AuxiliaryTransformExtractor.OutputDirectory";
    const string TargetPrefabPrefsKey = "MyUISystem.AuxiliaryTransformExtractor.TargetPrefab";

    AnimationClip source;
    AnimationClip bodyClip;
    GameObject model;
    DefaultAsset outputDirectory;
    bool excludeUpperArmTwist = true;
    bool excludePelvisTwist = true;
    bool combineWithBody;
    string report = "请选择源动画和目标 Humanoid 模型，然后预览。";
    Vector2 scroll;

    sealed class Plan
    {
        internal readonly List<EditorCurveBinding> sourceBindings = new List<EditorCurveBinding>();
        internal readonly List<EditorCurveBinding> targetBindings = new List<EditorCurveBinding>();
        internal readonly StringBuilder report = new StringBuilder();
        internal int sourcePaths;
        internal int acceptedPaths;
        internal int humanPaths;
        internal int twistPaths;
        internal int pelvisTwistPaths;
        internal int errors;
    }

    [MenuItem("Tools/MyUISystem/角色展示/动画/提取辅助 Transform", false, 342)]
    static void Open()
    {
        var window = GetWindow<AuxiliaryTransformClipExtractorWindow>("提取辅助 Transform");
        window.minSize = new Vector2(640, 430);
        window.UseSelection();
    }

    void OnEnable()
    {
        string path = EditorPrefs.GetString(OutputDirectoryPrefsKey, "");
        if (AssetDatabase.IsValidFolder(path))
            outputDirectory = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        model = AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefs.GetString(TargetPrefabPrefsKey, ""));
    }

    void UseSelection()
    {
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (selected is AnimationClip clip) source = clip;
            else if (selected is GameObject gameObject)
            {
                model = gameObject;
                string path = AssetDatabase.GetAssetPath(model) ?? "";
                if (EditorUtility.IsPersistent(model) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    EditorPrefs.SetString(TargetPrefabPrefsKey, path);
            }
        }
    }

    void OnGUI()
    {
        if (combineWithBody)
            EditorGUILayout.HelpBox("将身体动画与另一份动画中的辅助 Transform 合成新 Clip；不修改两份源资源。", MessageType.Info);
        else
            EditorGUILayout.HelpBox(
                "只生成新的 Generic 辅助动画；不复制 Animator 曲线、事件或修改源资源。" +
                "目标模型用于排除 Humanoid 骨骼并校验、重定位 Transform 路径。", MessageType.Info);
        if (GUILayout.Button("读取当前选择")) UseSelection();
        combineWithBody = EditorGUILayout.Toggle("组合为完整动画", combineWithBody);
        if (combineWithBody)
        {
            bodyClip = (AnimationClip)EditorGUILayout.ObjectField("Humanoid 身体动画", bodyClip, typeof(AnimationClip), false);
            source = (AnimationClip)EditorGUILayout.ObjectField("辅助曲线来源", source, typeof(AnimationClip), false);
        }
        else
            source = (AnimationClip)EditorGUILayout.ObjectField("源 AnimationClip", source, typeof(AnimationClip), false);
        EditorGUI.BeginChangeCheck();
        model = (GameObject)EditorGUILayout.ObjectField(
            "目标模型（Prefab 或场景）", model, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            string path = model == null ? "" : AssetDatabase.GetAssetPath(model) ?? "";
            if (model != null && EditorUtility.IsPersistent(model) &&
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                EditorPrefs.SetString(TargetPrefabPrefsKey, path);
            else
                EditorPrefs.DeleteKey(TargetPrefabPrefsKey);
        }
        excludeUpperArmTwist = EditorGUILayout.Toggle("排除 UpperArmTwist（建议）", excludeUpperArmTwist);
        excludePelvisTwist = EditorGUILayout.Toggle("排除 PelvisTwist CF（建议）", excludePelvisTwist);

        EditorGUI.BeginChangeCheck();
        DefaultAsset selectedDirectory = (DefaultAsset)EditorGUILayout.ObjectField(
            "输出目录（留空使用源目录）", outputDirectory, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck()) SetOutputDirectory(selectedDirectory);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("选择输出目录")) SelectOutputDirectory();
            if (GUILayout.Button("使用源目录")) SetOutputDirectory(null);
        }

        using (new EditorGUI.DisabledScope(source == null || model == null ||
                                           (combineWithBody && bodyClip == null) || EditorApplication.isCompiling ||
                                           EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (combineWithBody)
            {
                if (GUILayout.Button("预览组合与路径映射")) Process(false);
                if (GUILayout.Button("生成混合动画副本", GUILayout.Height(30))) Process(true);
            }
            else
            {
                if (GUILayout.Button("预览提取与路径映射")) Process(false);
                if (GUILayout.Button("生成辅助动画副本", GUILayout.Height(30))) Process(true);
            }
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        float height = Mathf.Max(220, EditorStyles.wordWrappedLabel.CalcHeight(
            new GUIContent(report), Mathf.Max(200, position.width - 40)));
        EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel, GUILayout.Height(height));
        EditorGUILayout.EndScrollView();
    }

    void SelectOutputDirectory()
    {
        string selected = EditorUtility.OpenFolderPanel("选择辅助动画输出目录", Application.dataPath, "");
        if (string.IsNullOrEmpty(selected)) return;
        string path = FileUtil.GetProjectRelativePath(selected).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(path))
        {
            report = "输出目录必须是项目 Assets 内已有的文件夹。";
            return;
        }
        SetOutputDirectory(AssetDatabase.LoadAssetAtPath<DefaultAsset>(path));
    }

    void SetOutputDirectory(DefaultAsset directory)
    {
        if (directory == null)
        {
            outputDirectory = null;
            EditorPrefs.DeleteKey(OutputDirectoryPrefsKey);
            return;
        }
        string path = AssetDatabase.GetAssetPath(directory);
        if (!AssetDatabase.IsValidFolder(path))
        {
            report = "请拖入 Project 窗口中的文件夹，而不是文件。";
            return;
        }
        outputDirectory = directory;
        EditorPrefs.SetString(OutputDirectoryPrefsKey, path);
    }

    void Process(bool generate)
    {
        try
        {
            string modelPath = AssetDatabase.GetAssetPath(model) ?? "";
            Plan plan = BuildPlan(source, model, excludeUpperArmTwist, excludePelvisTwist);
            if (combineWithBody) ValidateBodyClip(bodyClip, source, plan);
            if (combineWithBody)
            {
                string target = EditorUtility.IsPersistent(model)
                    ? modelPath
                    : model.scene.name + ": " + model.transform.root.name +
                      (model.transform == model.transform.root ? "" : "/" +
                       AnimationUtility.CalculateTransformPath(model.transform, model.transform.root));
                plan.report.Insert(0, "目标模型：" + target + "\n");
            }
            report = plan.report.ToString();
            if (!generate) return;
            if (plan.errors != 0 || plan.sourceBindings.Count == 0)
            {
                report += "存在阻断问题或没有可提取曲线，未生成资源。";
                return;
            }
            string path = combineWithBody
                ? SaveCombinedClip(bodyClip, source, plan, outputDirectory)
                : SaveClip(source, plan, outputDirectory);
            report += "\n已生成：" + path;
            Debug.Log("[提取辅助 Transform]\n" + report);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AnimationClip>(path));
        }
        catch (Exception exception)
        {
            report = "提取失败：" + exception;
            Debug.LogError("[提取辅助 Transform] " + exception);
        }
    }

    static Plan BuildPlan(AnimationClip clip, GameObject targetModel, bool excludeTwist, bool excludePelvis)
    {
        var plan = new Plan();
        Animator animator = targetModel.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            plan.errors++;
            plan.report.AppendLine("阻断：目标模型没有有效的 Humanoid Animator/Avatar。");
            return plan;
        }

        var humanNames = new HashSet<string>(
            animator.avatar.humanDescription.human.Select(bone => bone.boneName), StringComparer.Ordinal);
        var nodesByName = animator.GetComponentsInChildren<Transform>(true)
            .GroupBy(node => node.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var targetBindings = new HashSet<EditorCurveBinding>();
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip)
            .Where(binding => binding.type == typeof(Transform)).ToArray();

        foreach (IGrouping<string, EditorCurveBinding> group in bindings.GroupBy(binding => binding.path))
        {
            plan.sourcePaths++;
            string path = group.Key;
            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            if (humanNames.Contains(leaf))
            {
                plan.humanPaths++;
                plan.report.AppendLine("排除 Humanoid 骨骼：" + path);
                continue;
            }
            if (excludeTwist && IsUpperArmTwist(leaf))
            {
                plan.twistPaths++;
                plan.report.AppendLine("排除 UpperArmTwist：" + path);
                continue;
            }
            if (excludePelvis && IsPelvisTwist(leaf))
            {
                plan.pelvisTwistPaths++;
                plan.report.AppendLine("排除 PelvisTwist CF：" + path);
                continue;
            }
            if (string.IsNullOrEmpty(path) || path.Split('/').Any(segment =>
                    segment.StartsWith("path_", StringComparison.Ordinal)))
            {
                plan.errors++;
                plan.report.AppendLine("阻断：空路径或未解析路径：" + path);
                continue;
            }
            if (!nodesByName.TryGetValue(leaf, out Transform[] candidates) || candidates.Length != 1)
            {
                plan.errors++;
                plan.report.AppendLine($"阻断：目标节点匹配到 {candidates?.Length ?? 0} 个：{path}");
                continue;
            }

            string targetPath = AnimationUtility.CalculateTransformPath(candidates[0], animator.transform);
            // 同名节点必须唯一；路径变化由目标模型的真实层级决定，包括 PelvisTwist 的移动。
            if (targetPath != path)
                plan.report.AppendLine($"路径：{path} → {targetPath}");
            foreach (EditorCurveBinding binding in group)
            {
                EditorCurveBinding target = binding;
                target.path = targetPath;
                if (!targetBindings.Add(target))
                {
                    plan.errors++;
                    plan.report.AppendLine($"阻断：目标曲线重复：{targetPath}/{target.propertyName}");
                    continue;
                }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys.Any(key => !IsFinite(key.time) || !IsFinite(key.value)))
                {
                    plan.errors++;
                    plan.report.AppendLine($"阻断：曲线缺失或数值无效：{path}/{binding.propertyName}");
                    continue;
                }
                plan.sourceBindings.Add(binding);
                plan.targetBindings.Add(target);
            }
            plan.acceptedPaths++;
        }

        plan.report.Insert(0, $"源 Transform：{bindings.Length} 条曲线 / {plan.sourcePaths} 个节点；" +
            $"保留 {plan.targetBindings.Count} 条 / {plan.acceptedPaths} 个节点；" +
            $"排除 Humanoid {plan.humanPaths} 个、UpperArmTwist {plan.twistPaths} 个、" +
            $"PelvisTwist CF {plan.pelvisTwistPaths} 个；" +
            $"阻断 {plan.errors} 项。\n");
        return plan;
    }

    static void ValidateBodyClip(AnimationClip body, AnimationClip auxiliarySource, Plan plan)
    {
        if (body == auxiliarySource)
        {
            plan.errors++;
            plan.report.AppendLine("阻断：身体动画和辅助来源必须是两份不同的 Clip。");
            return;
        }

        EditorCurveBinding[] bodyBindings = AnimationUtility.GetCurveBindings(body);
        int animatorCount = bodyBindings.Count(binding => binding.type == typeof(Animator));
        if (!body.isHumanMotion || animatorCount == 0)
        {
            plan.errors++;
            plan.report.AppendLine("阻断：身体动画必须含有有效的 Humanoid Animator 曲线。");
        }

        float frameDuration = 1f / Mathf.Max(1f, Mathf.Min(body.frameRate, auxiliarySource.frameRate));
        if (Mathf.Abs(body.length - auxiliarySource.length) > frameDuration * 0.5f)
        {
            plan.errors++;
            plan.report.AppendLine($"阻断：两份动画时长不同（身体 {body.length:F3}s，辅助 {auxiliarySource.length:F3}s）。");
        }

        int discardedTransforms = bodyBindings.Count(binding => binding.type == typeof(Transform));
        plan.report.AppendLine($"组合时会从身体副本移除 {discardedTransforms} 条原有 Transform 曲线。");
        var occupied = new HashSet<EditorCurveBinding>(
            bodyBindings.Where(binding => binding.type != typeof(Transform)));
        foreach (EditorCurveBinding binding in plan.targetBindings)
        {
            if (!occupied.Contains(binding)) continue;
            plan.errors++;
            plan.report.AppendLine($"阻断：身体动画已有相同绑定：{binding.path}/{binding.propertyName}");
        }

        string[] components = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        EditorCurveBinding[] rootRotation = components.Select(component => bodyBindings.FirstOrDefault(binding =>
            binding.type == typeof(Animator) && binding.propertyName == component)).ToArray();
        if (rootRotation.All(binding => binding.type == typeof(Animator)))
        {
            float squaredLength = rootRotation.Sum(binding =>
            {
                float value = AnimationUtility.GetEditorCurve(body, binding).Evaluate(0f);
                return value * value;
            });
            if (!IsFinite(squaredLength) || squaredLength < 0.25f || squaredLength > 2.25f)
            {
                plan.errors++;
                plan.report.AppendLine("阻断：身体动画首帧 RootQ 不是有效旋转，请勿使用损坏的 Humanoid 曲线。");
            }
        }

        plan.report.AppendLine($"身体动画：{animatorCount} 条 Animator 曲线；组合阻断项累计 {plan.errors}。 ");
    }

    static bool IsUpperArmTwist(string name)
    {
        return name == "+UpperArmTwist L A01" || name == "+UpperArmTwist L A02" ||
               name == "+UpperArmTwist R A01" || name == "+UpperArmTwist R A02";
    }

    static bool IsPelvisTwist(string name) => name == "+PelvisTwist CF A01";

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static string SaveClip(AnimationClip sourceClip, Plan plan, DefaultAsset directoryAsset)
    {
        string sourcePath = AssetDatabase.GetAssetPath(sourceClip);
        string directory = directoryAsset == null
            ? Path.GetDirectoryName(sourcePath)?.Replace('\\', '/')
            : AssetDatabase.GetAssetPath(directoryAsset);
        if (string.IsNullOrEmpty(directory) || !AssetDatabase.IsValidFolder(directory))
            throw new InvalidOperationException("输出目录不在项目 Assets 中。");

        string safeName = sourceClip.name;
        foreach (char invalid in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
        string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + safeName + "_AuxiliaryOnly.anim");
        var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path), frameRate = sourceClip.frameRate };
        try
        {
            AnimationCurve[] curves = plan.sourceBindings
                .Select(binding => AnimationUtility.GetEditorCurve(sourceClip, binding)).ToArray();
            AnimationUtility.SetEditorCurves(clip, plan.targetBindings.ToArray(), curves);
            // 设置曲线可能改变 Clip 的结束时间；沿用源动画的播放区间与循环设置。
            AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(sourceClip));
            if (clip.isHumanMotion || AnimationUtility.GetCurveBindings(clip).Length != curves.Length)
                throw new InvalidOperationException("生成前校验失败：曲线数量或动画类型不符合预期。");
            AssetDatabase.CreateAsset(clip, path);
            clip = null;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (saved == null || saved.isHumanMotion || AnimationUtility.GetCurveBindings(saved).Length != curves.Length)
                throw new InvalidOperationException("资源已写入，但重导入校验失败：" + path);
            return path;
        }
        finally
        {
            if (clip != null) DestroyImmediate(clip);
        }
    }

    static string SaveCombinedClip(AnimationClip body, AnimationClip auxiliarySource, Plan plan,
        DefaultAsset directoryAsset)
    {
        string bodyPath = AssetDatabase.GetAssetPath(body);
        string directory = directoryAsset == null
            ? Path.GetDirectoryName(bodyPath)?.Replace('\\', '/')
            : AssetDatabase.GetAssetPath(directoryAsset);
        if (string.IsNullOrEmpty(directory) || !AssetDatabase.IsValidFolder(directory))
            throw new InvalidOperationException("输出目录不在项目 Assets 中。");

        string safeName = body.name;
        foreach (char invalid in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
        string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + safeName + "_WithAuxiliary.anim");
        EditorCurveBinding[] bodyBindings = AnimationUtility.GetCurveBindings(body);
        EditorCurveBinding[] oldTransforms = bodyBindings.Where(binding => binding.type == typeof(Transform)).ToArray();
        int expectedBindings = bodyBindings.Length - oldTransforms.Length + plan.targetBindings.Count;
        var combined = Instantiate(body);
        try
        {
            combined.name = Path.GetFileNameWithoutExtension(path);
            combined.hideFlags = HideFlags.None;
            // 身体源可能也带旧层级的辅助轨道；组合时只以新的路径映射为准。
            if (oldTransforms.Length != 0)
                AnimationUtility.SetEditorCurves(combined, oldTransforms, new AnimationCurve[oldTransforms.Length]);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(combined))
                if (binding.type == typeof(Transform)) AnimationUtility.SetObjectReferenceCurve(combined, binding, null);
            AnimationCurve[] curves = plan.sourceBindings
                .Select(binding => AnimationUtility.GetEditorCurve(auxiliarySource, binding)).ToArray();
            AnimationUtility.SetEditorCurves(combined, plan.targetBindings.ToArray(), curves);
            // 组合后继续采用身体动画的播放区间、循环设置及原有事件。
            AnimationUtility.SetAnimationClipSettings(combined, AnimationUtility.GetAnimationClipSettings(body));
            EditorCurveBinding[] combinedBindings = AnimationUtility.GetCurveBindings(combined);
            if (!combined.isHumanMotion || combinedBindings.Length != expectedBindings ||
                !new HashSet<EditorCurveBinding>(combinedBindings.Where(binding => binding.type == typeof(Transform)))
                    .SetEquals(plan.targetBindings))
                throw new InvalidOperationException("生成前校验失败：Humanoid 类型或组合曲线数量不符合预期。");
            AssetDatabase.CreateAsset(combined, path);
            combined = null;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            EditorCurveBinding[] savedBindings = saved == null
                ? Array.Empty<EditorCurveBinding>()
                : AnimationUtility.GetCurveBindings(saved);
            if (saved == null || !saved.isHumanMotion || savedBindings.Length != expectedBindings ||
                !new HashSet<EditorCurveBinding>(savedBindings.Where(binding => binding.type == typeof(Transform)))
                    .SetEquals(plan.targetBindings))
                throw new InvalidOperationException("资源已写入，但重导入校验失败：" + path);
            return path;
        }
        finally
        {
            if (combined != null) DestroyImmediate(combined);
        }
    }
}
