using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

internal sealed class ProfileModelRootAuditWindow : EditorWindow
{
    sealed class Row
    {
        internal ModelPreviewDefinition definition;
        internal string prefabPath;
        internal string sourcePath;
        internal string wrapperName;
        internal string status;
        internal bool selected;
        internal bool canRepair;
    }

    const string RootName = "ModelRoot";
    const string SkeletonName = "Bip001";

    ModelPreviewDatabase database;
    readonly List<Row> rows = new();
    Vector2 scroll;
    string report;

    [MenuItem("Tools/MyUISystem/角色展示/模型/检查 Profile 的 ModelRoot", false, 321)]
    static void Open()
    {
        var window = GetWindow<ProfileModelRootAuditWindow>("Profile 模型根检查");
        window.minSize = new Vector2(780, 440);
        window.FindDatabase();
    }

    void OnEnable()
    {
        FindDatabase();
    }

    void FindDatabase()
    {
        if (database != null) return;
        string[] guids = AssetDatabase.FindAssets("t:ModelPreviewDatabase");
        if (guids.Length == 1)
            database = AssetDatabase.LoadAssetAtPath<ModelPreviewDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "从预览数据库中的 Profile 找到 Model Address，再解析 Addressables 指向的 Prefab。" +
            "修复只针对勾选且检查通过的项；不会改变 Addressable 地址或 Prefab 路径。",
            MessageType.Info);
        database = (ModelPreviewDatabase)EditorGUILayout.ObjectField(
            "预览数据库", database, typeof(ModelPreviewDatabase), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("扫描数据库", GUILayout.Height(28))) Scan();
            if (GUILayout.Button("只勾选 Project 中选中的 Profile", GUILayout.Height(28)))
                SelectProjectProfiles();
            if (GUILayout.Button("勾选全部可修复项", GUILayout.Height(28)))
                foreach (Row row in rows) row.selected = row.canRepair;
            if (GUILayout.Button("取消勾选", GUILayout.Height(28)))
                foreach (Row row in rows) row.selected = false;
        }

        EditorGUILayout.LabelField($"已扫描 {rows.Count} 项；待修复 {rows.Count(row => row.canRepair)} 项；" +
            $"已勾选 {rows.Count(row => row.selected && row.canRepair)} 项。", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Row row in rows)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!row.canRepair))
                        row.selected = EditorGUILayout.Toggle(row.selected, GUILayout.Width(20));
                    EditorGUILayout.ObjectField(row.definition.PresentationProfile,
                        typeof(CharacterPresentationProfile), false, GUILayout.Width(185));
                    EditorGUILayout.LabelField(row.definition.TargetKey, GUILayout.Width(100));
                    EditorGUILayout.LabelField(row.definition.ModelAddress, GUILayout.Width(235));
                    EditorGUILayout.LabelField(row.status, EditorStyles.wordWrappedLabel);
                }
                if (!string.IsNullOrEmpty(row.prefabPath))
                    EditorGUILayout.SelectableLabel(row.prefabPath, EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(!rows.Any(row => row.selected && row.canRepair)))
        {
            if (GUILayout.Button("一键修复勾选的 Prefab", GUILayout.Height(32))) RepairSelected();
        }
        EditorGUILayout.HelpBox("Prefab 保存不能用 Ctrl+Z 撤销；请在修复后检查版本控制差异与动画表现。" +
            "无法唯一定位 Avatar、骨架或 Addressable 的项目只报告，不自动猜测。", MessageType.Warning);
        if (!string.IsNullOrEmpty(report))
            EditorGUILayout.HelpBox(report, MessageType.None);
    }

    void Scan()
    {
        rows.Clear();
        report = string.Empty;
        if (database == null)
        {
            report = "请先指定 ModelPreviewDatabase。";
            return;
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            report = "项目中找不到 Addressables Settings。";
            return;
        }

        using var serialized = new SerializedObject(database);
        SerializedProperty definitions = serialized.FindProperty("definitions");
        if (definitions == null || !definitions.isArray)
        {
            report = "预览数据库中找不到 definitions 列表。";
            return;
        }

        for (int i = 0; i < definitions.arraySize; i++)
        {
            var definition = definitions.GetArrayElementAtIndex(i).objectReferenceValue
                as ModelPreviewDefinition;
            if (definition == null || definition.PreviewType != ModelPreviewType.Character ||
                definition.PresentationProfile == null) continue;

            var row = new Row { definition = definition };
            Inspect(row, settings);
            rows.Add(row);
        }
        report = rows.Count == 0 ? "数据库中没有关联 Profile 的角色预览定义。" : string.Empty;
    }

    static void Inspect(Row row, AddressableAssetSettings settings)
    {
        string address = row.definition.ModelAddress;
        if (string.IsNullOrWhiteSpace(address))
        {
            row.status = "缺少 Model Address";
            return;
        }

        AddressableAssetEntry[] matches = settings.groups
            .Where(group => group != null)
            .SelectMany(group => group.entries)
            .Where(entry => entry != null && string.Equals(entry.address, address,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            row.status = matches.Length == 0 ? "未找到 Addressable" : "Addressable 地址不唯一";
            return;
        }

        row.prefabPath = AssetDatabase.GUIDToAssetPath(matches[0].guid);
        if (string.IsNullOrEmpty(row.prefabPath) ||
            !row.prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            row.status = "Addressable 不是 Prefab";
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.prefabPath);
        if (prefab == null)
        {
            row.status = "Prefab 无法加载";
            return;
        }

        try
        {
            Animator animator = FindRootAnimator(prefab);
            Transform wrapper = FindSkeletonWrapper(animator.transform);
            row.wrapperName = wrapper.name;
            if (wrapper.name == RootName)
            {
                row.status = animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman
                    ? "已是 ModelRoot/Bip001" : "层级正确，但 Avatar 无效";
                return;
            }

            row.sourcePath = FindSourceModelPath(animator, wrapper.name);
            row.canRepair = true;
            row.status = $"待修复：{wrapper.name} → ModelRoot";
        }
        catch (Exception exception)
        {
            row.status = "无法自动修复：" + exception.Message;
        }
    }

    static Animator FindRootAnimator(GameObject prefab)
    {
        Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
        if (animators.Length != 1 || animators[0].transform != prefab.transform)
            throw new InvalidOperationException("要求 Prefab 根上恰有一个 Animator");
        return animators[0];
    }

    static Transform FindSkeletonWrapper(Transform animatorRoot)
    {
        Transform[] wrappers = animatorRoot.Cast<Transform>()
            .Where(child => child.Find(SkeletonName) != null).ToArray();
        if (wrappers.Length != 1)
            throw new InvalidOperationException("找不到唯一的直接子节点 */Bip001");
        if (wrappers[0].name != RootName && animatorRoot.Find(RootName) != null)
            throw new InvalidOperationException("已存在另一个 ModelRoot 节点");
        return wrappers[0];
    }

    static string FindSourceModelPath(Animator animator, string wrapperName)
    {
        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("当前 Animator 没有有效 Humanoid Avatar");
        string path = AssetDatabase.GetAssetPath(animator.avatar);
        if (string.IsNullOrEmpty(path) ||
            !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Avatar 不是项目内 FBX 的子资产");

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null)
            throw new InvalidOperationException("无法加载 Avatar 对应的 FBX");
        Transform sourceWrapper = FindSkeletonWrapper(source.transform);
        if (sourceWrapper.name != wrapperName)
            throw new InvalidOperationException("Prefab 包装节点与 Avatar 来源 FBX 不一致");
        return path;
    }

    void SelectProjectProfiles()
    {
        var profiles = new HashSet<CharacterPresentationProfile>(
            Selection.objects.OfType<CharacterPresentationProfile>());
        foreach (Row row in rows)
            row.selected = row.canRepair && profiles.Contains(row.definition.PresentationProfile);
        report = profiles.Count == 0 ? "Project 中未选中 CharacterPresentationProfile。" : string.Empty;
    }

    void RepairSelected()
    {
        Row[] targets = rows.Where(row => row.selected && row.canRepair)
            .GroupBy(row => row.prefabPath, StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();
        if (targets.Length == 0) return;
        if (!EditorUtility.DisplayDialog("确认修复 ModelRoot",
            $"将修改 {targets.Length} 个现有 Prefab：只重命名骨架包装节点，并替换为对应 ModelRoot FBX 的 Avatar。" +
            "\nPrefab GUID、路径和 Addressable 地址保持不变。请确保可以通过版本控制回退。",
            "修复", "取消")) return;

        var messages = new List<string>();
        foreach (Row row in targets)
        {
            try
            {
                Repair(row);
                messages.Add("成功：" + row.prefabPath);
            }
            catch (Exception exception)
            {
                messages.Add("失败：" + row.prefabPath + " — " + exception.Message);
            }
        }
        AssetDatabase.SaveAssets();
        Scan();
        report = string.Join("\n", messages);
        Debug.Log("Profile ModelRoot 修复结果：\n" + report);
    }

    static void Repair(Row row)
    {
        // 先生成独立的标准化 FBX/Avatar；失败时不打开、更不保存目标 Prefab。
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(row.sourcePath);
        GameObject standardized = ModelRootStandardizerWindow.GetOrCreateStandardizedModel(
            source, out _);
        Avatar avatar = ModelRootStandardizerWindow.FindAvatar(AssetDatabase.GetAssetPath(standardized));
        if (avatar == null || !avatar.isValid || !avatar.isHuman ||
            standardized.transform.Find(RootName + "/" + SkeletonName) == null)
            throw new InvalidOperationException("标准化 FBX 的层级或 Humanoid Avatar 无效");

        string guid = AssetDatabase.AssetPathToGUID(row.prefabPath);
        GameObject contents = PrefabUtility.LoadPrefabContents(row.prefabPath);
        try
        {
            Animator animator = FindRootAnimator(contents);
            Transform wrapper = FindSkeletonWrapper(animator.transform);
            if (wrapper.name != row.wrapperName ||
                FindSourceModelPath(animator, wrapper.name) != row.sourcePath)
                throw new InvalidOperationException("扫描后 Prefab 已变化，请重新扫描");

            // 保留原节点、Renderer、组件与绑定，只替换路径名和与之匹配的 Avatar。
            wrapper.name = RootName;
            animator.avatar = avatar;
            PrefabUtility.SaveAsPrefabAsset(contents, row.prefabPath, out bool saved);
            if (!saved)
                throw new InvalidOperationException("Unity 未能保存 Prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        GameObject result = AssetDatabase.LoadAssetAtPath<GameObject>(row.prefabPath);
        Animator resultAnimator = result == null ? null : FindRootAnimator(result);
        if (resultAnimator == null ||
            resultAnimator.transform.Find(RootName + "/" + SkeletonName) == null ||
            resultAnimator.avatar != avatar ||
            AssetDatabase.AssetPathToGUID(row.prefabPath) != guid)
            throw new InvalidOperationException("保存后层级、Avatar 或 Prefab GUID 校验失败");
    }
}
