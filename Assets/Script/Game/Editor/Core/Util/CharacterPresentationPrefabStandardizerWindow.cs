using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class CharacterPresentationPrefabStandardizerWindow : EditorWindow
{
    const string OutputFolderPreference = "CharacterPresentationPrefabStandardizer.OutputFolder";
    const string MapNamePreference = "CharacterPresentationPrefabStandardizer.MapName";
    const string FallbackCliPreference = "CharacterPresentationPrefabStandardizer.UseFallbackCli";

    GameObject sourceModel;
    DefaultAsset outputFolder;
    string characterId;
    string slug;
    string bodyType;
    string weaponType;
    string teamConfig = "TeamConfig_01";
    string targetKey;
    string modelAddress;
    string specialIdle;
    string mapName;
    bool useFallbackCli;
    string report;
    Vector2 scroll;

    [MenuItem("Tools/MyUISystem/角色展示/自动化/完整接入/接入单个角色", false, 300)]
    static void Open()
    {
        var window = GetWindow<CharacterPresentationPrefabStandardizerWindow>("角色完整接入");
        window.minSize = new Vector2(580, 720);
        window.UseSelection();
    }

    void OnEnable()
    {
        string path = EditorPrefs.GetString(OutputFolderPreference, "");
        if (AssetDatabase.IsValidFolder(path))
            outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        mapName = EditorPrefs.GetString(MapNamePreference, string.Empty);
        if (string.IsNullOrWhiteSpace(mapName))
            mapName = "F:/AnimeStudio/PbiZPOuyDADBO80D/Maps/gi_7_0_0_live_48215702_47805902.map";
        useFallbackCli = EditorPrefs.GetBool(FallbackCliPreference, false);
        if (sourceModel != null && string.IsNullOrWhiteSpace(characterId))
            InferCharacterSettings();
    }

    void UseSelection()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        GameObject selected = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (selected != null)
        {
            sourceModel = selected;
            InferCharacterSettings();
        }
        if (sourceModel != null && outputFolder == null)
            SetOutputFolder(Path.GetDirectoryName(AssetDatabase.GetAssetPath(sourceModel))?.Replace('\\', '/'));
        report = "";
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "生成独立的角色展示 Prefab，不修改源 FBX、源 Prefab 或 Avatar。\n" +
            "规则：关闭 EffectMesh；将 +PelvisTwist CF A01 移到 Bip001 Pelvis 下并保持世界姿态；" +
            "关闭 AO_Bip001* 节点自身的 Renderer；保持 Bip001 启用。",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "“生成 Prefab 并登记角色”还会注册 Addressable、写入 Manifest，并调用现有资源准备流程。" +
            "模型名不能确定队伍动画和特殊待机，请在执行前确认这些字段。",
            MessageType.None);
        EditorGUILayout.HelpBox(
            "前置 Setup：原始 FBX 缺少配置时会自动调用 HoyoToon，修改源 FBX 导入设置、材质与贴图；" +
            "上方不修改源资产的说明仅指后续 Prefab 标准化阶段。",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        GameObject nextSource = (GameObject)EditorGUILayout.ObjectField(
            "源模型或 Prefab", sourceModel, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            sourceModel = nextSource;
            InferCharacterSettings();
        }
        if (GUILayout.Button("使用当前选择"))
            UseSelection();

        EditorGUI.BeginChangeCheck();
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("输出目录", outputFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
            RememberOutputFolder();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("选择目录"))
                ChooseOutputFolder();
            if (GUILayout.Button("使用源目录"))
                UseSourceFolder();
        }

        EditorGUILayout.LabelField("输出资产", GetOutputPath() ?? "请选择 Assets 内的源模型和输出目录");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("自动接入信息", EditorStyles.boldLabel);
        characterId = EditorGUILayout.TextField("角色 ID", characterId);
        slug = EditorGUILayout.TextField("资源短名", slug);
        bodyType = EditorGUILayout.TextField("体型", bodyType);
        weaponType = EditorGUILayout.TextField("武器类型", weaponType);
        teamConfig = EditorGUILayout.TextField("队伍动画", teamConfig);
        targetKey = EditorGUILayout.TextField("Target Key", targetKey);
        modelAddress = EditorGUILayout.TextField("Model Address", modelAddress);
        specialIdle = EditorGUILayout.TextField("特殊待机（逗号分隔）", specialIdle);
        if (GUILayout.Button("重新从模型名推断"))
            InferCharacterSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("缺失资源提取", EditorStyles.boldLabel);
        mapName = EditorGUILayout.TextField("AssetMap 文件", mapName);
        if (GUILayout.Button("选择 AssetMap 文件"))
            ChooseAssetMap();
        useFallbackCli = EditorGUILayout.Toggle("使用备用 AnimeStudio CLI", useFallbackCli);
        EditorGUILayout.HelpBox(
            "完整接入只向 AnimeStudio 请求当前角色缺少的资源；完整日志保存在 PresentationExtraction 的本次 run 目录。",
            MessageType.None);

        bool unavailable = EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || sourceModel == null ||
            CharacterPresentationAssetPipeline.IsCompleteFlowRunning;
        using (new EditorGUI.DisabledScope(unavailable))
        {
            if (GUILayout.Button("检查处理内容"))
                Analyze();
            using (new EditorGUI.DisabledScope(outputFolder == null))
            {
                if (GUILayout.Button("生成 / 更新展示 Prefab", GUILayout.Height(32)))
                    Generate();
            }
            using (new EditorGUI.DisabledScope(outputFolder == null && !IsExistingPresentationPrefab()))
            {
                if (GUILayout.Button("生成 Prefab 并完成角色接入", GUILayout.Height(36)))
                    CompleteOnboarding();
            }
        }
        if (CharacterPresentationAssetPipeline.IsCompleteFlowRunning)
            EditorGUILayout.HelpBox("资源提取正在后台运行，请等待完成。", MessageType.Info);

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(report ?? "", EditorStyles.wordWrappedLabel,
            GUILayout.MinHeight(190), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void Analyze()
    {
        try
        {
            report = CharacterPresentationAssetPipeline.InspectHoyoToonSetup(sourceModel,
                out bool needsMaterials, out bool needsRig);
            if (!needsRig)
                report += "\n" + CharacterPresentationPrefabStandardizer.Analyze(sourceModel);
            else
                report += "\n当前尚未完成骨架配置；点击生成时会先执行 Setup，再检查 Prefab 绑定。";
        }
        catch (Exception exception)
        {
            report = exception.Message;
        }
    }

    void Generate()
    {
        try
        {
            GeneratePrefab();
        }
        catch (Exception exception)
        {
            report = exception.Message;
            Debug.LogError("[展示 Prefab 标准化] " + report);
        }
    }

    string GeneratePrefab()
    {
        string outputPath = GetOutputPath();
        if (string.IsNullOrEmpty(outputPath))
            throw new InvalidOperationException("请选择 Assets 内的输出目录。");

        GameObject standardizedModel = CharacterPresentationAssetPipeline.PrepareModelForPresentation(
            sourceModel,
            out string modelReport);
        report = modelReport + "\n\n" +
            CharacterPresentationPrefabStandardizer.Generate(standardizedModel, outputPath);
        GameObject result = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
        EditorGUIUtility.PingObject(result);
        Debug.Log("[展示 Prefab 标准化] " + report);
        return outputPath;
    }

    void CompleteOnboarding()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mapName) || !File.Exists(mapName))
                throw new InvalidOperationException("请选择有效的 AssetMap 文件。");

            string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
            bool useExistingPresentation = IsExistingPresentationPrefab();
            string prefabPath = useExistingPresentation ? sourcePath : GeneratePrefab();
            if (useExistingPresentation)
                report = "使用现有展示 Prefab：" + prefabPath;

            string[] specialIdleNames = (specialIdle ?? string.Empty)
                .Split(new[] { ',', '，', ';', '；', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string onboardingReport = CharacterPresentationAssetPipeline.RegisterGeneratedModel(
                prefabPath,
                characterId,
                slug,
                bodyType,
                weaponType,
                teamConfig,
                targetKey,
                modelAddress,
                specialIdleNames);
            report += "\n\n" + onboardingReport;
            Debug.Log("[角色模型接入] " + onboardingReport);
            RememberExtractionSettings();
            CharacterPresentationAssetPipeline.StartCompleteCharacter(
                characterId,
                mapName,
                useFallbackCli,
                false,
                (_, pipelineReport) =>
                {
                    report = onboardingReport + "\n\n" + pipelineReport;
                    Repaint();
                });
            report += "\n\n缺失资源提取已启动；完成后会自动生成动画、表情、Profile 和 PreviewDefinition。";
        }
        catch (Exception exception)
        {
            report = exception.Message;
            Debug.LogError("[角色模型接入] " + report);
        }
    }

    bool IsExistingPresentationPrefab()
    {
        if (sourceModel == null) return false;
        string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
        return sourcePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
            sourceModel.name.EndsWith("_Presentation", StringComparison.OrdinalIgnoreCase);
    }

    void InferCharacterSettings()
    {
        if (sourceModel == null)
        {
            characterId = string.Empty;
            slug = string.Empty;
            bodyType = string.Empty;
            weaponType = string.Empty;
            targetKey = string.Empty;
            modelAddress = string.Empty;
            specialIdle = string.Empty;
            return;
        }

        string modelName = sourceModel.name.Trim();
        modelName = Regex.Replace(modelName, @"\(Clone\)$", string.Empty, RegexOptions.IgnoreCase).Trim();
        string previousName;
        do
        {
            previousName = modelName;
            modelName = Regex.Replace(
                modelName,
                @"_(Presentation|ModelRoot|Fixed)(?:\s*\d+)?$",
                string.Empty,
                RegexOptions.IgnoreCase);
        } while (!string.Equals(previousName, modelName, StringComparison.Ordinal));
        modelName = Regex.Replace(modelName, @"\s+\d+$", string.Empty).Trim();

        Match match = Regex.Match(
            modelName,
            @"^Avatar_(?<body>[^_]+)_(?<weapon>[^_]+)_(?<id>.+)$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            report = "无法从模型名推断角色信息。名称应类似 Avatar_Girl_Sword_Furina；请手工填写接入信息。";
            return;
        }

        bodyType = match.Groups["body"].Value;
        weaponType = match.Groups["weapon"].Value;
        characterId = match.Groups["id"].Value;
        slug = characterId.ToLowerInvariant();
        targetKey = slug;
        modelAddress = "model/character/" + slug;
        teamConfig = "TeamConfig_01";
        specialIdle = string.Empty;
        report = $"已从模型名推断：{characterId} / {bodyType} / {weaponType}\n" +
            "TeamConfig_01 是当前默认候选；完整接入前请确认该角色实际使用的队伍动画。";
    }

    string GetOutputPath()
    {
        if (sourceModel == null || outputFolder == null) return null;
        string folderPath = AssetDatabase.GetAssetPath(outputFolder);
        if (!AssetDatabase.IsValidFolder(folderPath)) return null;
        return $"{folderPath}/{sourceModel.name}_Presentation.prefab";
    }

    void UseSourceFolder()
    {
        if (sourceModel == null) return;
        string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
        SetOutputFolder(Path.GetDirectoryName(sourcePath)?.Replace('\\', '/'));
    }

    void ChooseOutputFolder()
    {
        string initialPath = outputFolder == null ? Application.dataPath : AssetDatabase.GetAssetPath(outputFolder);
        if (!Path.IsPathRooted(initialPath))
            initialPath = Path.GetFullPath(initialPath);
        string selectedPath = EditorUtility.OpenFolderPanel("选择展示 Prefab 输出目录", initialPath, "");
        if (string.IsNullOrEmpty(selectedPath)) return;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
        string normalized = Path.GetFullPath(selectedPath).Replace('\\', '/').TrimEnd('/');
        if (!normalized.StartsWith(projectRoot + "/Assets", StringComparison.OrdinalIgnoreCase))
        {
            report = "输出目录必须位于当前项目的 Assets 内。";
            return;
        }
        SetOutputFolder(normalized.Substring(projectRoot.Length + 1));
    }

    void ChooseAssetMap()
    {
        string initialPath = File.Exists(mapName) ? Path.GetDirectoryName(mapName) : Application.dataPath;
        string selectedPath = EditorUtility.OpenFilePanel("选择 AssetMap 文件", initialPath, "map");
        if (string.IsNullOrEmpty(selectedPath)) return;
        mapName = Path.GetFullPath(selectedPath);
        RememberExtractionSettings();
    }

    void SetOutputFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path)) return;
        outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        RememberOutputFolder();
    }

    void RememberOutputFolder()
    {
        string path = outputFolder == null ? "" : AssetDatabase.GetAssetPath(outputFolder);
        if (AssetDatabase.IsValidFolder(path))
            EditorPrefs.SetString(OutputFolderPreference, path);
    }

    void RememberExtractionSettings()
    {
        EditorPrefs.SetString(MapNamePreference, mapName ?? string.Empty);
        EditorPrefs.SetBool(FallbackCliPreference, useFallbackCli);
    }
}

internal static class CharacterPresentationPrefabStandardizer
{
    const string EffectMeshName = "EffectMesh";
    const string PelvisName = "Bip001 Pelvis";
    const string PelvisTwistName = "+PelvisTwist CF A01";
    const string SkeletonRootName = "Bip001";
    const string AoMeshPrefix = "AO_Bip001";

    internal static string Analyze(GameObject sourceModel)
    {
        ValidateSource(sourceModel);
        Transform[] nodes = sourceModel.GetComponentsInChildren<Transform>(true);
        Transform pelvis = FindUnique(nodes, PelvisName, false);
        Transform pelvisTwist = FindUnique(nodes, PelvisTwistName, false);
        Transform skeletonRoot = FindUnique(nodes, SkeletonRootName, true);
        int effectMeshCount = nodes.Count(node => node.name == EffectMeshName);
        Renderer[] aoRenderers = FindAoRenderers(nodes);
        Animator animator = FindAnimator(sourceModel);
        CharacterPreviewActor[] actors = sourceModel.GetComponentsInChildren<CharacterPreviewActor>(true);
        if (actors.Length > 1)
            throw new InvalidOperationException("模型中存在多个 CharacterPreviewActor，工具不会猜测目标。");
        List<SkinnedMeshRenderer> faceRenderers = FindFaceRenderers(sourceModel);

        var lines = new List<string>
        {
            $"源资产：{AssetDatabase.GetAssetPath(sourceModel)}",
            $"Bip001：{PathOf(skeletonRoot, sourceModel.transform)}（当前 {(skeletonRoot.gameObject.activeSelf ? "启用" : "关闭")}）",
            $"EffectMesh：{effectMeshCount} 个，将设为关闭",
            $"AO_Bip001 Renderer：{aoRenderers.Length} 个，将只关闭 Renderer，不关闭骨架",
            actors.Length == 0
                ? $"CharacterPreviewActor：将添加到 {PathOf(animator.transform, sourceModel.transform)}"
                : $"CharacterPreviewActor：将刷新 {PathOf(actors[0].transform, sourceModel.transform)} 上的绑定",
            $"FaceRenderers：可绑定 {faceRenderers.Count}/3（{DescribeFaceRenderers(faceRenderers)}）",
        };

        if (pelvisTwist == null)
        {
            lines.Add("PelvisTwist：未找到，将跳过移动");
        }
        else
        {
            if (pelvis == null)
                throw new InvalidOperationException($"找到了 {PelvisTwistName}，但找不到 {PelvisName}，无法安全修改父级。");
            lines.Add($"PelvisTwist：{PathOf(pelvisTwist, sourceModel.transform)}");
            lines.Add(pelvisTwist.parent == pelvis
                ? "PelvisTwist 已在 Bip001 Pelvis 下，不会重复移动"
                : $"PelvisTwist 将移动到 {PathOf(pelvis, sourceModel.transform)} 下，并保持世界姿态");
        }

        lines.Add("注意：移动 PelvisTwist 会改变对应 Generic/Transform 动画的绑定路径，但不会修改 Humanoid Avatar。");
        return string.Join("\n", lines);
    }

    internal static string Generate(GameObject sourceModel, string outputPath)
    {
        ValidateSource(sourceModel);
        ValidateOutputPath(sourceModel, outputPath);

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(sourceModel, previewScene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("无法实例化源模型。");
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

            Transform[] nodes = instance.GetComponentsInChildren<Transform>(true);
            Animator animator = FindAnimator(instance);
            if (animator.transform.Find(ModelRootStandardizerPostprocessor.RootName) == null)
                throw new InvalidOperationException("Animator 下找不到直接子节点 ModelRoot，拒绝生成路径不兼容的展示 Prefab。");
            Transform skeletonRoot = FindUnique(nodes, SkeletonRootName, true);
            skeletonRoot.gameObject.SetActive(true);

            int disabledEffectMeshes = 0;
            foreach (Transform node in nodes)
            {
                if (node.name != EffectMeshName) continue;
                node.gameObject.SetActive(false);
                disabledEffectMeshes++;
            }

            int disabledAoRenderers = 0;
            foreach (Renderer renderer in FindAoRenderers(nodes))
            {
                renderer.enabled = false;
                disabledAoRenderers++;
            }

            bool movedPelvisTwist = MovePelvisTwist(nodes);
            CharacterPreviewActor actor = BindActor(instance);
            int faceRendererCount = actor.FaceRenderers.Length;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            if (saved == null)
                throw new InvalidOperationException("Unity 未能保存展示 Prefab。");

            AssetDatabase.SaveAssets();
            VerifySavedPrefab(outputPath);
            return $"已生成：{outputPath}\n" +
                $"EffectMesh 已关闭：{disabledEffectMeshes} 个\n" +
                $"AO Renderer 已关闭：{disabledAoRenderers} 个\n" +
                $"PelvisTwist：{(movedPelvisTwist ? "已移动到 Bip001 Pelvis 下" : "无需移动或模型中不存在")}\n" +
                $"CharacterPreviewActor：已绑定 Animator 和 {faceRendererCount}/3 个面部 Renderer\n" +
                "源资产和 Avatar 未修改。请继续验证 Humanoid 动画及包含 PelvisTwist 曲线的辅助动画。";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    static bool MovePelvisTwist(Transform[] nodes)
    {
        Transform pelvisTwist = FindUnique(nodes, PelvisTwistName, false);
        if (pelvisTwist == null) return false;
        Transform pelvis = FindUnique(nodes, PelvisName, true);
        if (pelvisTwist.parent == pelvis) return false;
        if (pelvisTwist.IsChildOf(pelvis))
            throw new InvalidOperationException("PelvisTwist 已在 Pelvis 的更深层级，工具不会猜测应如何折叠层级。");

        Vector3 worldPosition = pelvisTwist.position;
        Quaternion worldRotation = pelvisTwist.rotation;
        Vector3 worldScale = pelvisTwist.lossyScale;
        pelvisTwist.SetParent(pelvis, true);

        if (Vector3.Distance(worldPosition, pelvisTwist.position) > 0.00001f ||
            Quaternion.Angle(worldRotation, pelvisTwist.rotation) > 0.001f ||
            Vector3.Distance(worldScale, pelvisTwist.lossyScale) > 0.001f)
            throw new InvalidOperationException("移动 PelvisTwist 后无法保持世界姿态，已停止保存。请检查父级是否存在非均匀缩放。");
        return true;
    }

    static Renderer[] FindAoRenderers(IEnumerable<Transform> nodes)
    {
        return nodes
            .Where(node => node.name.StartsWith(AoMeshPrefix, StringComparison.Ordinal))
            .SelectMany(node => node.GetComponents<Renderer>())
            .Distinct()
            .ToArray();
    }

    static CharacterPreviewActor BindActor(GameObject root)
    {
        Animator animator = FindAnimator(root);
        CharacterPreviewActor[] actors = root.GetComponentsInChildren<CharacterPreviewActor>(true);
        if (actors.Length > 1)
            throw new InvalidOperationException("模型中存在多个 CharacterPreviewActor，工具不会猜测目标。");
        if (actors.Length == 1 && actors[0].gameObject != animator.gameObject)
            throw new InvalidOperationException("CharacterPreviewActor 与 Animator 不在同一节点，工具不会擅自移动组件。");

        CharacterPreviewActor actor = actors.Length == 0
            ? animator.gameObject.AddComponent<CharacterPreviewActor>()
            : actors[0];
        List<SkinnedMeshRenderer> faceRenderers = FindFaceRenderers(root);

        using var serialized = new SerializedObject(actor);
        SerializedProperty animatorProperty = serialized.FindProperty("animator");
        SerializedProperty faceRenderersProperty = serialized.FindProperty("faceRenderers");
        if (animatorProperty == null || faceRenderersProperty == null)
            throw new InvalidOperationException("CharacterPreviewActor 的序列化字段与工具预期不一致。");

        animatorProperty.objectReferenceValue = animator;
        faceRenderersProperty.arraySize = faceRenderers.Count;
        for (int i = 0; i < faceRenderers.Count; i++)
            faceRenderersProperty.GetArrayElementAtIndex(i).objectReferenceValue = faceRenderers[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return actor;
    }

    static Animator FindAnimator(GameObject root)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        if (animators.Length == 0)
            throw new InvalidOperationException("模型中找不到 Animator，无法创建 CharacterPreviewActor 绑定。");
        if (animators.Length > 1)
            throw new InvalidOperationException("模型中存在多个 Animator，工具不会猜测展示 Animator。");
        return animators[0];
    }

    static List<SkinnedMeshRenderer> FindFaceRenderers(GameObject root)
    {
        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var results = new List<SkinnedMeshRenderer>(3);
        TryAddFaceRenderer(renderers, "Brow", "Brow_", results);
        TryAddFaceRenderer(renderers, "Face", "Mouth_", results);
        TryAddFaceRenderer(renderers, "Face_Eye", "Eye_", results);
        return results;
    }

    static void TryAddFaceRenderer(
        IEnumerable<SkinnedMeshRenderer> renderers,
        string objectName,
        string blendShapePrefix,
        ICollection<SkinnedMeshRenderer> results)
    {
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer.name != objectName || renderer.sharedMesh == null) continue;
            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
            {
                if (!renderer.sharedMesh.GetBlendShapeName(i).StartsWith(blendShapePrefix,
                    StringComparison.Ordinal)) continue;
                results.Add(renderer);
                return;
            }
        }
    }

    static string DescribeFaceRenderers(IEnumerable<SkinnedMeshRenderer> renderers)
    {
        string result = string.Join("、", renderers.Select(renderer => renderer.name));
        return string.IsNullOrEmpty(result) ? "未找到" : result;
    }

    static void VerifySavedPrefab(string outputPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
        if (prefab == null)
            throw new InvalidOperationException("保存后无法重新加载展示 Prefab。");

        Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true);
        Transform skeletonRoot = FindUnique(nodes, SkeletonRootName, true);
        if (!skeletonRoot.gameObject.activeSelf)
            throw new InvalidOperationException("保存后的 Bip001 仍处于关闭状态。");
        if (nodes.Any(node => node.name == EffectMeshName && node.gameObject.activeSelf))
            throw new InvalidOperationException("保存后的 EffectMesh 仍处于启用状态。");
        if (FindAoRenderers(nodes).Any(renderer => renderer.enabled))
            throw new InvalidOperationException("保存后仍存在启用的 AO_Bip001 Renderer。");

        Transform pelvisTwist = FindUnique(nodes, PelvisTwistName, false);
        if (pelvisTwist != null && (pelvisTwist.parent == null || pelvisTwist.parent.name != PelvisName))
            throw new InvalidOperationException("保存后的 PelvisTwist 父级不是 Bip001 Pelvis。");

        Animator animator = FindAnimator(prefab);
        if (animator.transform.Find(ModelRootStandardizerPostprocessor.RootName) == null)
            throw new InvalidOperationException("保存后的 Animator 下找不到直接子节点 ModelRoot。");
        if (animator.avatar != null && !animator.avatar.isValid)
            throw new InvalidOperationException("保存后的 Animator 引用了无效 Avatar。");
        CharacterPreviewActor[] actors = prefab.GetComponentsInChildren<CharacterPreviewActor>(true);
        if (actors.Length != 1 || actors[0].gameObject != animator.gameObject || actors[0].Animator != animator)
            throw new InvalidOperationException("保存后的 CharacterPreviewActor 或 Animator 绑定无效。");
        List<SkinnedMeshRenderer> expectedFaceRenderers = FindFaceRenderers(prefab);
        if (!actors[0].FaceRenderers.SequenceEqual(expectedFaceRenderers))
            throw new InvalidOperationException("保存后的 CharacterPreviewActor 面部 Renderer 绑定不完整或顺序错误。");
    }

    static void ValidateSource(GameObject sourceModel)
    {
        if (sourceModel == null)
            throw new InvalidOperationException("请选择源模型或 Prefab。");
        string path = AssetDatabase.GetAssetPath(sourceModel);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
            (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
             !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("源对象必须是当前项目 Assets 内的 FBX 或 Prefab 资产。");
    }

    static void ValidateOutputPath(GameObject sourceModel, string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath) ||
            !outputPath.StartsWith("Assets/", StringComparison.Ordinal) ||
            !outputPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("输出必须是当前项目 Assets 内的 .prefab 路径。");
        string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
        if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("输出路径不能覆盖源资产。");
        string folder = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            throw new InvalidOperationException("输出目录不存在。");
    }

    static Transform FindUnique(IEnumerable<Transform> nodes, string name, bool required)
    {
        Transform[] matches = nodes.Where(node => node.name == name).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"模型中存在多个同名节点 {name}，工具不会猜测目标。");
        if (matches.Length == 0 && required)
            throw new InvalidOperationException($"模型中找不到必要节点：{name}");
        return matches.FirstOrDefault();
    }

    static string PathOf(Transform node, Transform root)
    {
        string path = AnimationUtility.CalculateTransformPath(node, root);
        return string.IsNullOrEmpty(path) ? root.name : path;
    }
}
