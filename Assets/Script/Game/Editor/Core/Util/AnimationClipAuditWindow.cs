using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal sealed class AnimationClipAuditWindow : EditorWindow
{
    readonly List<AnimationClip> clips = new List<AnimationClip>();
    readonly List<AnimationClipAuditResult> results = new List<AnimationClipAuditResult>();
    GameObject validationModel;
    Vector2 scrollPosition;
    string collectionReport = "请从 Project 多选 AnimationClip、FBX 或文件夹，也可以直接拖入下方区域。";

    [MenuItem("Tools/MyUISystem/角色展示/动画/检查 AnimationClip", false, 341)]
    static void Open()
    {
        var window = GetWindow<AnimationClipAuditWindow>("动画检查");
        window.minSize = new Vector2(680, 460);
        window.UseSelection();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "只读检查 AnimationClip，不会修改资源。支持多选 AnimationClip、FBX 和文件夹；文件夹会递归检查其中所有动画。",
            MessageType.Info);

        DrawDropArea();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("读取 Project 当前选择"))
                UseSelection();
            if (GUILayout.Button("清空", GUILayout.Width(80)))
                Clear();
        }

        validationModel = (GameObject)EditorGUILayout.ObjectField(
            "验证模型（可选）", validationModel, typeof(GameObject), false);
        EditorGUILayout.LabelField("待检查动画", clips.Count.ToString());
        EditorGUILayout.HelpBox(collectionReport, MessageType.None);

        using (new EditorGUI.DisabledScope(
                   clips.Count == 0 || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("开始检查", GUILayout.Height(32)))
                Audit();
        }

        if (results.Count == 0)
            return;

        DrawSummary();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (AnimationClipAuditResult result in results)
            DrawResult(result);
        EditorGUILayout.EndScrollView();
    }

    void DrawDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0, 54, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "拖入 AnimationClip、FBX 或文件夹（支持多选）", EditorStyles.helpBox);

        Event current = Event.current;
        if (!dropArea.Contains(current.mousePosition) ||
            (current.type != UnityEngine.EventType.DragUpdated &&
             current.type != UnityEngine.EventType.DragPerform))
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type == UnityEngine.EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            CollectFromObjects(DragAndDrop.objectReferences, false);
        }
        current.Use();
    }

    void UseSelection()
    {
        CollectFromObjects(Selection.objects, true);
    }

    void CollectFromObjects(IEnumerable<UnityEngine.Object> objects, bool replace)
    {
        if (replace)
            clips.Clear();

        int previousCount = clips.Count;
        var uniqueClips = new HashSet<AnimationClip>(clips);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (UnityEngine.Object item in objects ?? Array.Empty<UnityEngine.Object>())
        {
            if (item == null)
                continue;

            if (item is AnimationClip directClip)
                uniqueClips.Add(directClip);

            string path = AssetDatabase.GetAssetPath(item);
            if (!string.IsNullOrEmpty(path))
                paths.Add(path.Replace('\\', '/'));
        }

        foreach (string path in paths)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { path }))
                    AddClipsAtPath(AssetDatabase.GUIDToAssetPath(guid), uniqueClips);
            }
            else
            {
                AddClipsAtPath(path, uniqueClips);
            }
        }

        clips.Clear();
        clips.AddRange(uniqueClips.OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
            .ThenBy(clip => clip.name, StringComparer.Ordinal));
        results.Clear();
        collectionReport = $"已收集 {clips.Count} 个动画，本次新增 {Math.Max(0, clips.Count - previousCount)} 个。";
        Repaint();
    }

    static void AddClipsAtPath(string path, ISet<AnimationClip> destination)
    {
        if (string.IsNullOrEmpty(path))
            return;

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip)
                destination.Add(clip);
    }

    void Clear()
    {
        clips.Clear();
        results.Clear();
        collectionReport = "已清空。";
    }

    void Audit()
    {
        results.Clear();
        GameObject validationRoot = GetValidationRoot(validationModel);
        try
        {
            for (int index = 0; index < clips.Count; index++)
            {
                AnimationClip clip = clips[index];
                EditorUtility.DisplayProgressBar(
                    "检查 AnimationClip", clip == null ? "无效动画" : clip.name, (float)index / clips.Count);
                results.Add(AnimationClipAuditor.Audit(clip, validationRoot));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        int normalCount = results.Count(result => result.Status == AnimationClipAuditStatus.Normal);
        int warningCount = results.Count(result => result.Status == AnimationClipAuditStatus.Warning);
        int errorCount = results.Count(result => result.Status == AnimationClipAuditStatus.Error);
        collectionReport = $"检查完成：正常 {normalCount}，有警告 {warningCount}，异常 {errorCount}。";
        Debug.Log("[AnimationClip 检查]\n" + BuildTextReport());
    }

    static GameObject GetValidationRoot(GameObject model)
    {
        if (model == null)
            return null;

        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.GetComponentInChildren<Animator>(true);
        return animator == null ? model : animator.gameObject;
    }

    void DrawSummary()
    {
        int normalCount = results.Count(result => result.Status == AnimationClipAuditStatus.Normal);
        int warningCount = results.Count(result => result.Status == AnimationClipAuditStatus.Warning);
        int errorCount = results.Count(result => result.Status == AnimationClipAuditStatus.Error);
        MessageType messageType = errorCount > 0
            ? MessageType.Error
            : warningCount > 0 ? MessageType.Warning : MessageType.Info;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.HelpBox(
                $"正常 {normalCount}　有警告 {warningCount}　异常 {errorCount}", messageType);
            if (GUILayout.Button("复制完整报告", GUILayout.Width(110), GUILayout.Height(38)))
                EditorGUIUtility.systemCopyBuffer = BuildTextReport();
        }
    }

    void DrawResult(AnimationClipAuditResult result)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string status = result.Status == AnimationClipAuditStatus.Normal
                    ? "正常"
                    : result.Status == AnimationClipAuditStatus.Warning ? "有警告" : "异常";
                result.Expanded = EditorGUILayout.Foldout(
                    result.Expanded, $"[{status}] {result.ClipName}", true, EditorStyles.foldoutHeader);
                if (result.Clip != null && GUILayout.Button("定位", GUILayout.Width(54)))
                {
                    Selection.activeObject = result.Clip;
                    EditorGUIUtility.PingObject(result.Clip);
                }
            }

            if (!result.Expanded)
                return;

            EditorGUILayout.LabelField("资源", result.AssetPath);
            EditorGUILayout.LabelField(
                "数据",
                $"时长 {result.Length:0.###}s / {result.FrameRate:0.###} FPS / " +
                $"绑定 {result.FloatBindingCount + result.ObjectBindingCount} / 路径 {result.UniquePathCount} / " +
                $"关键帧 {result.KeyCount}");
            EditorGUILayout.LabelField(
                "类型",
                $"{(result.IsHumanMotion ? "Humanoid" : "Generic/Transform")} / " +
                $"Loop Time {(result.LoopTime ? "开启" : "关闭")}");

            if (result.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("未发现异常或集成警告。", MessageType.Info);
                return;
            }

            foreach (AnimationClipAuditIssue issue in result.Issues)
            {
                MessageType type = issue.Severity == AnimationClipAuditSeverity.Error
                    ? MessageType.Error
                    : issue.Severity == AnimationClipAuditSeverity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Message}\n位置：{issue.Location}", type);
            }
        }
    }

    string BuildTextReport()
    {
        var report = new StringBuilder();
        report.AppendLine(collectionReport);
        foreach (AnimationClipAuditResult result in results)
        {
            report.AppendLine();
            report.AppendLine($"【{result.Status}】{result.ClipName}");
            report.AppendLine(result.AssetPath);
            report.AppendLine(
                $"时长={result.Length:0.######}，FPS={result.FrameRate:0.###}，" +
                $"FloatBindings={result.FloatBindingCount}，ObjectBindings={result.ObjectBindingCount}，" +
                $"Paths={result.UniquePathCount}，Keys={result.KeyCount}，" +
                $"HumanMotion={result.IsHumanMotion}，LoopTime={result.LoopTime}");
            foreach (AnimationClipAuditIssue issue in result.Issues)
                report.AppendLine($"- [{issue.Severity}/{issue.Code}] {issue.Message} | {issue.Location}");
        }
        return report.ToString();
    }
}

internal static class AnimationClipAuditor
{
    const string StandardRootName = "ModelRoot";
    const string PelvisTwistName = "+PelvisTwist CF A01";
    const string PelvisName = "Bip001 Pelvis";

    internal static AnimationClipAuditResult Audit(AnimationClip clip, GameObject validationRoot = null)
    {
        var result = new AnimationClipAuditResult(clip);
        if (clip == null)
        {
            result.AddError("NULL_CLIP", "动画引用无效。", "AnimationClip");
            return result;
        }

        EditorCurveBinding[] floatBindings = AnimationUtility.GetCurveBindings(clip);
        EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        result.FloatBindingCount = floatBindings.Length;
        result.ObjectBindingCount = objectBindings.Length;
        result.IsHumanMotion = clip.isHumanMotion;
        result.Length = clip.length;
        result.FrameRate = clip.frameRate;
        result.LoopTime = AnimationUtility.GetAnimationClipSettings(clip).loopTime;

        if (floatBindings.Length + objectBindings.Length == 0)
            result.AddError("EMPTY_CLIP", "动画没有任何可播放绑定。", result.AssetPath);
        if (!IsFinite(clip.length) || clip.length < 0f)
            result.AddError("INVALID_LENGTH", "动画时长不是有效数值。", result.AssetPath);
        if (!IsFinite(clip.frameRate) || clip.frameRate <= 0f)
            result.AddError("INVALID_FRAME_RATE", "动画帧率不是有效正数。", result.AssetPath);
        if (clip.name.IndexOf("Loop", StringComparison.OrdinalIgnoreCase) >= 0 && !result.LoopTime)
            result.AddWarning("LOOP_DISABLED", "名称包含 Loop，但 Loop Time 未开启。", result.AssetPath);

        var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
        var transformPaths = new HashSet<string>(StringComparer.Ordinal);
        var unresolvedPaths = new HashSet<string>(StringComparer.Ordinal);
        var invalidPelvisPaths = new HashSet<string>(StringComparer.Ordinal);
        var invalidCurveBindings = new HashSet<string>(StringComparer.Ordinal);

        foreach (EditorCurveBinding binding in floatBindings)
        {
            RegisterPath(binding, uniquePaths, transformPaths, unresolvedPaths, invalidPelvisPaths);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
            {
                result.AddError("MISSING_CURVE", "绑定存在，但无法读取曲线。", FormatBinding(binding));
                continue;
            }

            result.KeyCount += curve.length;
            Keyframe[] keys = curve.keys;
            for (int index = 0; index < keys.Length; index++)
            {
                Keyframe key = keys[index];
                // Infinity tangent 是 Unity 合法的阶梯曲线表示，只有 NaN tangent 才属于损坏数据。
                if (IsFinite(key.time) && IsFinite(key.value) && !float.IsNaN(key.inTangent) &&
                    !float.IsNaN(key.outTangent) && IsFinite(key.inWeight) && IsFinite(key.outWeight))
                    continue;

                string location = $"{FormatBinding(binding)}，关键帧 {index}，t={key.time}";
                if (invalidCurveBindings.Add(location))
                    result.AddError("NON_FINITE_KEY", "关键帧包含 NaN 或非法 Infinity。", location);
            }
        }

        foreach (EditorCurveBinding binding in objectBindings)
            RegisterPath(binding, uniquePaths, transformPaths, unresolvedPaths, invalidPelvisPaths);

        result.UniquePathCount = uniquePaths.Count;
        foreach (string path in unresolvedPaths)
            result.AddError("UNRESOLVED_PATH", "绑定仍包含未解析的 path_* 节点。", path);
        foreach (string path in invalidPelvisPaths)
            result.AddWarning(
                "PELVIS_TWIST_PARENT",
                $"{PelvisTwistName} 不在 {PelvisName} 下，运行时层级可能无法匹配。",
                path);

        AddRootWarnings(result, transformPaths);
        if (validationRoot != null)
            ValidateAgainstModel(result, floatBindings, objectBindings, validationRoot);
        return result;
    }

    static void RegisterPath(
        EditorCurveBinding binding,
        ISet<string> uniquePaths,
        ISet<string> transformPaths,
        ISet<string> unresolvedPaths,
        ISet<string> invalidPelvisPaths)
    {
        string path = binding.path ?? string.Empty;
        uniquePaths.Add(path);
        if (binding.type == typeof(Transform) && !string.IsNullOrEmpty(path))
            transformPaths.Add(path);
        if (ContainsUnresolvedSegment(path))
            unresolvedPaths.Add(path);
        if (binding.type == typeof(Transform) && HasInvalidPelvisParent(path))
            invalidPelvisPaths.Add(path);
    }

    static void AddRootWarnings(AnimationClipAuditResult result, IEnumerable<string> transformPaths)
    {
        string[] paths = transformPaths.ToArray();
        if (paths.Length == 0)
            return;

        var roots = new HashSet<string>(paths.Select(GetFirstSegment), StringComparer.Ordinal);
        string[] characterRoots = roots.Where(root =>
                root.StartsWith("Avatar_", StringComparison.Ordinal) && root != StandardRootName)
            .OrderBy(root => root, StringComparer.Ordinal)
            .ToArray();
        foreach (string root in characterRoots)
            result.AddWarning(
                "CHARACTER_ROOT_PREFIX",
                $"Transform 路径仍使用角色根 {root}，尚未转换为 {StandardRootName}/。",
                root + "/");

        if (roots.Contains(StandardRootName) && roots.Count > 1)
            result.AddWarning(
                "MIXED_ROOT_PREFIX",
                "动画同时包含 ModelRoot 和其他顶层路径，请确认 Animator 根及转换结果。",
                string.Join(", ", roots.OrderBy(root => root, StringComparer.Ordinal)));
    }

    static void ValidateAgainstModel(
        AnimationClipAuditResult result,
        IEnumerable<EditorCurveBinding> floatBindings,
        IEnumerable<EditorCurveBinding> objectBindings,
        GameObject validationRoot)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        foreach (EditorCurveBinding binding in floatBindings.Concat(objectBindings))
        {
            if (binding.type == typeof(Animator) && string.IsNullOrEmpty(binding.path))
                continue;

            try
            {
                if (AnimationUtility.GetAnimatedObject(validationRoot, binding) == null)
                    missing.Add($"{binding.path} ({binding.type.Name})");
            }
            catch (Exception exception)
            {
                result.AddError(
                    "MODEL_VALIDATION_FAILED",
                    "验证模型绑定时发生异常：" + exception.Message,
                    FormatBinding(binding));
            }
        }

        foreach (string location in missing.OrderBy(value => value, StringComparer.Ordinal))
            result.AddError("MODEL_PATH_MISSING", "验证模型中找不到绑定目标。", location);

        Animator animator = validationRoot.GetComponent<Animator>();
        if (result.IsHumanMotion || animator == null || animator.avatar == null || !animator.avatar.isHuman)
            return;

        var humanBoneNames = new HashSet<string>(
            animator.avatar.humanDescription.human.Select(bone => bone.boneName), StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (EditorCurveBinding binding in floatBindings)
        {
            if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path))
                continue;
            string nodeName = GetLastSegment(binding.path);
            if (humanBoneNames.Contains(nodeName))
                conflicts.Add(binding.path);
        }

        foreach (string path in conflicts.OrderBy(value => value, StringComparer.Ordinal))
            result.AddWarning(
                "HUMANOID_GENERIC_CONFLICT",
                "Generic Transform 曲线控制了 Humanoid Avatar 已映射的骨骼，Animator 运行时可能忽略该曲线。",
                path);
    }

    static bool ContainsUnresolvedSegment(string path)
    {
        return path.Split('/').Any(segment => segment.StartsWith("path_", StringComparison.OrdinalIgnoreCase));
    }

    static bool HasInvalidPelvisParent(string path)
    {
        if (!string.Equals(GetLastSegment(path), PelvisTwistName, StringComparison.Ordinal))
            return false;
        string[] segments = path.Split('/');
        return segments.Length < 2 || !string.Equals(segments[segments.Length - 2], PelvisName, StringComparison.Ordinal);
    }

    static string GetFirstSegment(string path)
    {
        int separator = path.IndexOf('/');
        return separator < 0 ? path : path.Substring(0, separator);
    }

    static string GetLastSegment(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? path : path.Substring(separator + 1);
    }

    static string FormatBinding(EditorCurveBinding binding)
    {
        string path = string.IsNullOrEmpty(binding.path) ? "<Animator Root>" : binding.path;
        return $"{path} [{binding.type.Name}.{binding.propertyName}]";
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

internal enum AnimationClipAuditStatus
{
    Normal,
    Warning,
    Error
}

internal enum AnimationClipAuditSeverity
{
    Info,
    Warning,
    Error
}

internal sealed class AnimationClipAuditIssue
{
    internal AnimationClipAuditSeverity Severity;
    internal string Code;
    internal string Message;
    internal string Location;
}

internal sealed class AnimationClipAuditResult
{
    internal readonly AnimationClip Clip;
    internal readonly string ClipName;
    internal readonly string AssetPath;
    internal readonly List<AnimationClipAuditIssue> Issues = new List<AnimationClipAuditIssue>();
    internal int FloatBindingCount;
    internal int ObjectBindingCount;
    internal int UniquePathCount;
    internal int KeyCount;
    internal float Length;
    internal float FrameRate;
    internal bool IsHumanMotion;
    internal bool LoopTime;
    internal bool Expanded = true;

    internal AnimationClipAuditStatus Status => Issues.Any(issue => issue.Severity == AnimationClipAuditSeverity.Error)
        ? AnimationClipAuditStatus.Error
        : Issues.Any(issue => issue.Severity == AnimationClipAuditSeverity.Warning)
            ? AnimationClipAuditStatus.Warning
            : AnimationClipAuditStatus.Normal;

    internal AnimationClipAuditResult(AnimationClip clip)
    {
        Clip = clip;
        ClipName = clip == null ? "<Null>" : clip.name;
        AssetPath = clip == null ? "<无资源路径>" : AssetDatabase.GetAssetPath(clip);
    }

    internal void AddWarning(string code, string message, string location)
    {
        AddIssue(AnimationClipAuditSeverity.Warning, code, message, location);
    }

    internal void AddError(string code, string message, string location)
    {
        AddIssue(AnimationClipAuditSeverity.Error, code, message, location);
    }

    void AddIssue(AnimationClipAuditSeverity severity, string code, string message, string location)
    {
        Issues.Add(new AnimationClipAuditIssue
        {
            Severity = severity,
            Code = code,
            Message = message,
            Location = string.IsNullOrEmpty(location) ? "<未指定>" : location
        });
    }
}
