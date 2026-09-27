using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

internal static class CharacterPresentationProfileGenerator
{
    const string ManifestPath = "Tools/PresentationAssetManifest.json";

    [Serializable]
    sealed class Manifest
    {
        public int schemaVersion;
        public ProjectSettings project;
        public CharacterSettings[] characters;
    }

    [Serializable]
    sealed class ProjectSettings
    {
        public string animationRuntimeRoot;
        public string facePresetRoot;
        public string presentationProfileRoot;
        public string previewDefinitionRoot;
        public string modelPreviewDatabase;
        public string defaultCameraPreset;
    }

    [Serializable]
    sealed class CharacterSettings
    {
        public string id;
        public string slug;
        public string bodyType;
        public string weaponType;
        public string teamConfig;
        public string targetKey;
        public string modelAddress;
        public string sourceModel;
        public string runtimeClipSuffix;
        public string cameraPreset;
    }

    // Editor and runtime compile separately; persist enum names through SerializedProperty.
    enum FaceSource
    {
        Preset,
        Embedded,
        None
    }

    sealed class PhaseAssets
    {
        public AnimationClip Body;
        public AnimationClip Auxiliary;
        public FaceExpressionPreset Face;
        public FaceSource FaceSource;
    }

    sealed class StateAssets
    {
        public PhaseAssets Enter;
        public PhaseAssets Loop;
        public PhaseAssets Exit;
    }

    sealed class CharacterPlan
    {
        public CharacterSettings Settings;
        public string TargetKey;
        public string ProfilePath;
        public string PreviewPath;
        public CameraPreset CameraPreset;
        public StateAssets Default;
        public StateAssets TeamSwitch;
        public StateAssets WeaponShow;
        public StateAssets RelicShow;
    }

    [MenuItem("Tools/MyUISystem/角色展示/Profile/校验资源清单", false, 310)]
    static void ValidateManifest()
    {
        BuildPlans(null, out _, out List<CharacterPlan> plans, out List<string> errors,
            out List<string> warnings);
        LogResult("角色展示 Profile 清单校验", plans, errors, warnings);
    }

    [MenuItem("Tools/MyUISystem/角色展示/Profile/从资源清单生成 Profile", false, 311)]
    internal static void GenerateProfiles()
    {
        GenerateProfiles(null);
    }

    internal static void GenerateProfile(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            throw new ArgumentException("必须指定角色 ID。", nameof(characterId));
        GenerateProfiles(characterId);
    }

    static void GenerateProfiles(string characterId)
    {
        BuildPlans(characterId, out Manifest manifest, out List<CharacterPlan> plans, out List<string> errors,
            out List<string> warnings);
        if (errors.Count > 0)
        {
            LogResult("角色展示 Profile 生成已取消", plans, errors, warnings);
            return;
        }

        // 目标角色先完整解析成功再写资产，避免批量生成留下只有部分字段的 Profile。
        EnsureFolder(manifest.project.presentationProfileRoot);
        EnsureFolder(manifest.project.previewDefinitionRoot);

        var previewDefinitions = new List<ModelPreviewDefinition>(plans.Count);
        foreach (CharacterPlan plan in plans)
        {
            CharacterPresentationProfile profile = CreateOrLoad<CharacterPresentationProfile>(plan.ProfilePath);
            ApplyProfile(profile, plan);

            ModelPreviewDefinition preview = CreateOrLoad<ModelPreviewDefinition>(plan.PreviewPath);
            ApplyPreviewDefinition(preview, profile, plan);
            previewDefinitions.Add(preview);
        }

        ModelPreviewDatabase database = AssetDatabase.LoadAssetAtPath<ModelPreviewDatabase>(
            manifest.project.modelPreviewDatabase);
        ApplyDatabase(database, previewDefinitions);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LogResult("角色展示 Profile 生成完成", plans, errors, warnings);
        if (warnings.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Profile 已生成，但存在缺失资源",
                $"已生成 {plans.Count} 个角色 Profile，缺失 {warnings.Count} 项。空缺字段已保持为空，完整清单请查看 Console。",
                "确定");
        }
        if (plans.Count > 0)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<CharacterPresentationProfile>(
                plans[0].ProfilePath);
        }
    }

    static void BuildPlans(
        string characterId,
        out Manifest manifest,
        out List<CharacterPlan> plans,
        out List<string> errors,
        out List<string> warnings)
    {
        plans = new List<CharacterPlan>();
        errors = new List<string>();
        warnings = new List<string>();
        manifest = LoadManifest(errors);
        if (manifest == null)
        {
            return;
        }

        ValidateProjectSettings(manifest.project, errors);
        if (manifest.project == null)
        {
            return;
        }

        ModelPreviewDatabase database = AssetDatabase.LoadAssetAtPath<ModelPreviewDatabase>(
            manifest.project.modelPreviewDatabase);
        if (database == null)
        {
            errors.Add($"找不到 ModelPreviewDatabase: {manifest.project.modelPreviewDatabase}");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var targetKeys = new HashSet<string>(StringComparer.Ordinal);
        CharacterSettings[] allCharacters = manifest.characters ?? Array.Empty<CharacterSettings>();
        CharacterSettings[] selectedCharacters = string.IsNullOrWhiteSpace(characterId)
            ? allCharacters
            : allCharacters.Where(character => character != null &&
                string.Equals(character.id, characterId, StringComparison.Ordinal)).ToArray();
        if (!string.IsNullOrWhiteSpace(characterId) && selectedCharacters.Length != 1)
        {
            errors.Add($"角色 ID 必须唯一匹配一个角色：{characterId}，当前匹配数 {selectedCharacters.Length}。");
        }

        foreach (CharacterSettings character in selectedCharacters)
        {
            int errorCountBefore = errors.Count;
            ValidateCharacterSettings(character, ids, targetKeys, errors);
            if (character == null)
            {
                continue;
            }

            CharacterPlan plan = BuildCharacterPlan(manifest.project, character, errors, warnings);
            if (errors.Count == errorCountBefore)
            {
                plans.Add(plan);
            }
        }

        if (database != null)
        {
            ValidateDatabaseTargets(database, plans, errors);
        }

        if (allCharacters.Length == 0)
        {
            errors.Add("Manifest 没有配置任何角色。");
        }
    }

    static Manifest LoadManifest(List<string> errors)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        string fullPath = string.IsNullOrEmpty(projectRoot)
            ? string.Empty
            : Path.Combine(projectRoot, ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            errors.Add($"找不到 Manifest: {ManifestPath}");
            return null;
        }

        try
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(fullPath));
            if (manifest == null || manifest.schemaVersion != 1)
            {
                errors.Add("PresentationAssetManifest 的 schemaVersion 必须为 1。");
                return null;
            }

            return manifest;
        }
        catch (Exception exception)
        {
            errors.Add($"读取 Manifest 失败: {exception.Message}");
            return null;
        }
    }

    static void ValidateProjectSettings(ProjectSettings project, List<string> errors)
    {
        if (project == null)
        {
            errors.Add("Manifest 缺少 project 配置。");
            return;
        }

        RequireAssetFolder(project.animationRuntimeRoot, "animationRuntimeRoot", errors);
        RequireAssetFolder(project.facePresetRoot, "facePresetRoot", errors);
        RequireAssetPath(project.presentationProfileRoot, "presentationProfileRoot", errors);
        RequireAssetPath(project.previewDefinitionRoot, "previewDefinitionRoot", errors);
        RequireAssetPath(project.modelPreviewDatabase, "modelPreviewDatabase", errors);
        RequireAssetPath(project.defaultCameraPreset, "defaultCameraPreset", errors);
    }

    static void ValidateCharacterSettings(
        CharacterSettings character,
        HashSet<string> ids,
        HashSet<string> targetKeys,
        List<string> errors)
    {
        if (character == null)
        {
            errors.Add("Manifest 包含空角色配置。");
            return;
        }

        RequireValue(character.id, "character.id", errors);
        RequireValue(character.slug, $"{character.id}.slug", errors);
        RequireValue(character.bodyType, $"{character.id}.bodyType", errors);
        RequireValue(character.weaponType, $"{character.id}.weaponType", errors);
        RequireValue(character.modelAddress, $"{character.id}.modelAddress", errors);
        RequireValue(character.runtimeClipSuffix, $"{character.id}.runtimeClipSuffix", errors);

        string targetKey = GetTargetKey(character);
        if (!string.IsNullOrEmpty(character.id) && !ids.Add(character.id))
        {
            errors.Add($"角色 ID 重复: {character.id}");
        }

        if (!string.IsNullOrEmpty(targetKey) && !targetKeys.Add(targetKey))
        {
            errors.Add($"角色 targetKey 重复: {targetKey}");
        }
    }

    static CharacterPlan BuildCharacterPlan(
        ProjectSettings project,
        CharacterSettings character,
        List<string> errors,
        List<string> warnings)
    {
        string id = character.id ?? string.Empty;
        string body = character.bodyType ?? string.Empty;
        string weapon = character.weaponType ?? string.Empty;
        string characterPrefix = $"Ani_Avatar_{body}_{weapon}_{id}";
        string runtimeRoot = project.animationRuntimeRoot;
        string faceRoot = project.facePresetRoot;
        string suffix = character.runtimeClipSuffix ?? string.Empty;

        var plan = new CharacterPlan
        {
            Settings = character,
            TargetKey = GetTargetKey(character),
            ProfilePath = Join(project.presentationProfileRoot, $"{id}_Presentation.asset"),
            PreviewPath = Join(project.previewDefinitionRoot, $"{id}_Preview.asset"),
            CameraPreset = RequireAsset<CameraPreset>(
                string.IsNullOrEmpty(character.cameraPreset)
                    ? project.defaultCameraPreset
                    : character.cameraPreset,
                $"{id}.CameraPreset",
                errors)
        };
        ValidateOutputAssetType<CharacterPresentationProfile>(plan.ProfilePath, errors);
        ValidateOutputAssetType<ModelPreviewDefinition>(plan.PreviewPath, errors);

        plan.Default = new StateAssets
        {
            Loop = new PhaseAssets
            {
                Body = OptionalClip(
                    Join(runtimeRoot, "Common", body, "Default"),
                    $"Ani_Avatar_{body}_Standby",
                    suffix,
                    $"{id}.Default.Loop.Body",
                    warnings),
                Auxiliary = OptionalClip(
                    Join(runtimeRoot, "Character", id, "Default", "Auxiliary"),
                    $"{characterPrefix}_Standby",
                    suffix,
                    $"{id}.Default.Loop.Auxiliary",
                    warnings),
                Face = OptionalAsset<FaceExpressionPreset>(
                    Join(faceRoot, "Common", "Face_Normal_01.asset"),
                    $"{id}.Default.Loop.Face",
                    warnings)
            }
        };

        plan.WeaponShow = new StateAssets
        {
            Enter = BuildWeaponPhase(project, character, "BS", "Enter", warnings),
            Loop = BuildWeaponPhase(project, character, "Loop", "Loop", warnings),
            Exit = BuildWeaponPhase(project, character, "AS", "Exit", warnings)
        };

        plan.RelicShow = new StateAssets
        {
            Enter = BuildRelicPhase(project, character, "BS", "Enter", warnings),
            Loop = BuildRelicPhase(project, character, "Loop", "Loop", warnings),
            Exit = BuildRelicPhase(project, character, "AS", "Exit", warnings)
        };

        if (!string.IsNullOrWhiteSpace(character.teamConfig))
        {
            plan.TeamSwitch = new StateAssets
            {
                Enter = BuildTeamPhase(project, character, "BS", "Enter", warnings),
                Loop = BuildTeamPhase(project, character, "Loop", "Loop", warnings),
                Exit = BuildTeamPhase(project, character, "AS", "Exit", warnings)
            };
        }
        else
        {
            plan.TeamSwitch = new StateAssets();
        }

        GameObject sourceModel = string.IsNullOrEmpty(character.sourceModel) ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(character.sourceModel);
        // Characters registered from an existing prefab may only have an Addressable address.
        if (sourceModel == null)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings == null ? null : settings.groups
                .Where(group => group != null)
                .SelectMany(group => group.entries)
                .SingleOrDefault(asset => asset.address == character.modelAddress);
            if (entry != null)
                sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
        }
        foreach (StateAssets state in new[] { plan.Default, plan.TeamSwitch, plan.WeaponShow, plan.RelicShow })
        {
            foreach (PhaseAssets phase in new[] { state.Enter, state.Loop, state.Exit })
            {
                if (phase == null || phase.Face != null) continue;
                phase.FaceSource = HasUsableEmbeddedFace(phase.Body, phase.Auxiliary, sourceModel,
                    out string faceIssue)
                    ? FaceSource.Embedded : FaceSource.None;
                if (phase.FaceSource == FaceSource.None)
                    warnings.Add($"{id}: {phase.Body?.name ?? phase.Auxiliary?.name ?? "空阶段"} 缺少可用表情：{faceIssue}");
            }
        }

        return plan;
    }

    static bool HasUsableEmbeddedFace(AnimationClip body, AnimationClip auxiliary,
        GameObject sourceModel, out string issue)
    {
        issue = "动画没有面部曲线";
        if (sourceModel == null)
        {
            issue = "无法读取源模型以验证 BlendShape";
            return false;
        }
        SkinnedMeshRenderer[] renderers = sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int matched = 0;
        foreach (AnimationClip clip in new[] { body, auxiliary })
        {
            if (clip == null) continue;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(SkinnedMeshRenderer) ||
                    !binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)) continue;
                string shape = binding.propertyName.Substring("blendShape.".Length);
                int slash = binding.path.LastIndexOf('/');
                string rendererName = binding.path.Substring(slash + 1);
                if (!binding.path.StartsWith("ModelRoot/", StringComparison.Ordinal) ||
                    uint.TryParse(shape, out _) ||
                    !renderers.Any(renderer => renderer.name == rendererName &&
                        renderer.sharedMesh != null && renderer.sharedMesh.GetBlendShapeIndex(shape) >= 0))
                {
                    issue = $"未解析或不匹配 {binding.path}/{shape}";
                    return false;
                }
                matched++;
            }
        }
        return matched > 0;
    }

    static PhaseAssets BuildWeaponPhase(
        ProjectSettings project,
        CharacterSettings character,
        string sourcePhase,
        string profilePhase,
        List<string> warnings)
    {
        string body = character.bodyType;
        string weapon = character.weaponType;
        string id = character.id;
        string characterPrefix = $"Ani_Avatar_{body}_{weapon}_{id}";
        return new PhaseAssets
        {
            Body = OptionalClip(
                Join(project.animationRuntimeRoot, "Common", body, "Weapon", weapon),
                $"Ani_Avatar_{body}_{weapon}_UI_WeaponShow{sourcePhase}",
                character.runtimeClipSuffix,
                $"{id}.WeaponShow.{profilePhase}.Body",
                warnings),
            Auxiliary = OptionalClip(
                Join(project.animationRuntimeRoot, "Character", id, "Weapon", "Auxiliary"),
                $"{characterPrefix}_UI_WeaponShow{sourcePhase}",
                character.runtimeClipSuffix,
                $"{id}.WeaponShow.{profilePhase}.Auxiliary",
                warnings),
            Face = OptionalAsset<FaceExpressionPreset>(
                Join(project.facePresetRoot, "Equip", weapon,
                    $"Face_Common_{body}_{weapon}_UI_ShowWeapon{sourcePhase}.asset"),
                $"{id}.WeaponShow.{profilePhase}.Face",
                warnings)
        };
    }

    static PhaseAssets BuildRelicPhase(
        ProjectSettings project,
        CharacterSettings character,
        string sourcePhase,
        string profilePhase,
        List<string> warnings)
    {
        string body = character.bodyType;
        string weapon = character.weaponType;
        string id = character.id;
        string characterPrefix = $"Ani_Avatar_{body}_{weapon}_{id}";
        return new PhaseAssets
        {
            Body = OptionalClip(
                Join(project.animationRuntimeRoot, "Common", body, "Relic"),
                $"Ani_Avatar_{body}_RelicShow{sourcePhase}",
                character.runtimeClipSuffix,
                $"{id}.RelicShow.{profilePhase}.Body",
                warnings),
            Auxiliary = OptionalClip(
                Join(project.animationRuntimeRoot, "Character", id, "Relic", "Auxiliary"),
                $"{characterPrefix}_RelicShow{sourcePhase}",
                character.runtimeClipSuffix,
                $"{id}.RelicShow.{profilePhase}.Auxiliary",
                warnings),
            Face = OptionalAsset<FaceExpressionPreset>(
                Join(project.facePresetRoot, "Relic", $"Face_Common_RelicShow_{sourcePhase}.asset"),
                $"{id}.RelicShow.{profilePhase}.Face",
                warnings)
        };
    }

    static PhaseAssets BuildTeamPhase(
        ProjectSettings project,
        CharacterSettings character,
        string sourcePhase,
        string profilePhase,
        List<string> warnings)
    {
        string id = character.id;
        string characterPrefix = $"Ani_Avatar_{character.bodyType}_{character.weaponType}_{id}";
        return new PhaseAssets
        {
            Body = OptionalClip(
                Join(project.animationRuntimeRoot, "Character", id, "TeamSwitch", "FullBody"),
                $"{characterPrefix}_{character.teamConfig}_{sourcePhase}",
                character.runtimeClipSuffix,
                $"{id}.TeamSwitch.{profilePhase}.Body",
                warnings),
            Face = OptionalAsset<FaceExpressionPreset>(
                Join(project.facePresetRoot, id,
                    $"Face_{id}_{character.teamConfig}{sourcePhase}.asset"),
                $"{id}.TeamSwitch.{profilePhase}.Face",
                warnings)
        };
    }

    static AnimationClip OptionalClip(
        string folder,
        string sourceName,
        string suffix,
        string role,
        List<string> warnings)
    {
        return OptionalAsset<AnimationClip>(
            Join(folder, sourceName + suffix + ".anim"), role, warnings);
    }

    static T OptionalAsset<T>(string path, string role, List<string> warnings)
        where T : UnityEngine.Object
    {
        T asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            warnings.Add($"{role}: 缺少 {typeof(T).Name}，Profile 对应字段将保持为空，路径 {path}");
        return asset;
    }

    static T RequireAsset<T>(string path, string role, List<string> errors)
        where T : UnityEngine.Object
    {
        T asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            errors.Add($"{role}: 找不到 {typeof(T).Name}，路径 {path}");
        }

        return asset;
    }

    static void ApplyProfile(CharacterPresentationProfile profile, CharacterPlan plan)
    {
        var serialized = new SerializedObject(profile);
        SetState(serialized.FindProperty("defaultPresentation"), plan.Default);
        SetState(serialized.FindProperty("teamSwitch"), plan.TeamSwitch);
        SetState(serialized.FindProperty("weaponShow"), plan.WeaponShow);
        SetState(serialized.FindProperty("relicShow"), plan.RelicShow);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    static void SetState(SerializedProperty property, StateAssets state)
    {
        SetPhase(property, "enter", state.Enter);
        SetPhase(property, "loop", state.Loop);
        SetPhase(property, "exit", state.Exit);
    }

    static void SetPhase(SerializedProperty state, string phaseName, PhaseAssets phase)
    {
        state.FindPropertyRelative(phaseName + "BodyClip").objectReferenceValue = phase?.Body;
        state.FindPropertyRelative(phaseName + "AuxiliaryClip").objectReferenceValue = phase?.Auxiliary;
        state.FindPropertyRelative(phaseName + "FaceA").objectReferenceValue = phase?.Face;
        state.FindPropertyRelative(phaseName + "FaceB").objectReferenceValue = null;
        SerializedProperty faceSource = state.FindPropertyRelative(phaseName + "FaceSource");
        string sourceName = (phase?.FaceSource ?? FaceSource.Preset).ToString();
        int sourceIndex = Array.IndexOf(faceSource.enumNames, sourceName);
        if (sourceIndex < 0)
            throw new InvalidOperationException($"Unknown presentation face source: {sourceName}");
        faceSource.enumValueIndex = sourceIndex;
    }

    static void ApplyPreviewDefinition(
        ModelPreviewDefinition preview,
        CharacterPresentationProfile profile,
        CharacterPlan plan)
    {
        var serialized = new SerializedObject(preview);
        bool isNew = string.IsNullOrEmpty(preview.TargetKey);
        serialized.FindProperty("previewType").enumValueIndex = (int)ModelPreviewType.Character;
        serialized.FindProperty("targetKey").stringValue = plan.TargetKey;
        serialized.FindProperty("modelAddress").stringValue = plan.Settings.modelAddress;
        serialized.FindProperty("cameraPreset").objectReferenceValue = plan.CameraPreset;
        serialized.FindProperty("presentationProfile").objectReferenceValue = profile;
        if (isNew)
        {
            // 新建定义使用中性布局；已有角色的人工构图数据必须保持不动。
            serialized.FindProperty("localPosition").vector3Value = Vector3.zero;
            serialized.FindProperty("localEulerAngles").vector3Value = Vector3.zero;
            serialized.FindProperty("localScale").vector3Value = Vector3.one;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preview);
    }

    static void ApplyDatabase(
        ModelPreviewDatabase database,
        IReadOnlyList<ModelPreviewDefinition> previews)
    {
        var serialized = new SerializedObject(database);
        SerializedProperty definitions = serialized.FindProperty("definitions");
        foreach (ModelPreviewDefinition preview in previews)
        {
            bool found = false;
            for (int index = 0; index < definitions.arraySize; index++)
            {
                ModelPreviewDefinition existing =
                    definitions.GetArrayElementAtIndex(index).objectReferenceValue as ModelPreviewDefinition;
                if (existing == preview)
                {
                    found = true;
                    break;
                }

                if (existing != null && existing.TargetKey == preview.TargetKey)
                {
                    throw new InvalidOperationException(
                        $"ModelPreviewDatabase 已存在相同 targetKey: {preview.TargetKey}");
                }
            }

            if (!found)
            {
                int index = definitions.arraySize;
                definitions.InsertArrayElementAtIndex(index);
                definitions.GetArrayElementAtIndex(index).objectReferenceValue = preview;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
    }

    static void ValidateDatabaseTargets(
        ModelPreviewDatabase database,
        IReadOnlyList<CharacterPlan> plans,
        List<string> errors)
    {
        var serialized = new SerializedObject(database);
        SerializedProperty definitions = serialized.FindProperty("definitions");
        foreach (CharacterPlan plan in plans)
        {
            ModelPreviewDefinition plannedPreview =
                AssetDatabase.LoadAssetAtPath<ModelPreviewDefinition>(plan.PreviewPath);
            for (int index = 0; index < definitions.arraySize; index++)
            {
                ModelPreviewDefinition existing =
                    definitions.GetArrayElementAtIndex(index).objectReferenceValue as ModelPreviewDefinition;
                if (existing != null && existing != plannedPreview && existing.TargetKey == plan.TargetKey)
                {
                    errors.Add(
                        $"{plan.Settings.id}: ModelPreviewDatabase 中 targetKey 已被其他资源占用: " +
                        plan.TargetKey);
                }
            }
        }
    }

    static void ValidateOutputAssetType<T>(string path, List<string> errors)
        where T : UnityEngine.Object
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null && !(existing is T))
        {
            errors.Add($"输出路径已被其他类型资源占用: {path}");
        }
    }

    static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        string normalized = path?.Trim().Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"输出目录必须位于 Assets 内: {path}");
        }

        string current = "Assets";
        string[] parts = normalized.Substring("Assets/".Length).Split('/');
        foreach (string part in parts)
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, part);
            }
            current = next;
        }
    }

    static void RequireAssetFolder(string value, string field, List<string> errors)
    {
        RequireAssetPath(value, field, errors);
        if (!string.IsNullOrEmpty(value) && !AssetDatabase.IsValidFolder(value))
        {
            errors.Add($"{field} 目录不存在: {value}");
        }
    }

    static void RequireAssetPath(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)
            || (value != "Assets" && !value.StartsWith("Assets/", StringComparison.Ordinal)))
        {
            errors.Add($"{field} 必须是 Assets 下的项目路径: {value}");
        }
    }

    static void RequireValue(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Manifest 缺少必填字段: {field}");
        }
    }

    static string GetTargetKey(CharacterSettings character)
    {
        return string.IsNullOrWhiteSpace(character.targetKey)
            ? character.slug
            : character.targetKey;
    }

    static string Join(params string[] parts)
    {
        var result = new StringBuilder();
        foreach (string rawPart in parts)
        {
            string part = rawPart?.Trim().Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (result.Length > 0)
            {
                result.Append('/');
            }
            result.Append(part);
        }
        return result.ToString();
    }

    static void LogResult(
        string title,
        IReadOnlyCollection<CharacterPlan> plans,
        List<string> errors,
        List<string> warnings)
    {
        var report = new StringBuilder();
        report.AppendLine(title);
        report.AppendLine($"可生成角色: {plans.Count}，缺失资源: {warnings.Count}，错误: {errors.Count}");
        foreach (CharacterPlan plan in plans)
        {
            report.AppendLine($"[OK] {plan.Settings.id}: {plan.ProfilePath}");
        }
        foreach (string error in errors)
        {
            report.AppendLine("[ERROR] " + error);
        }
        foreach (string warning in warnings)
        {
            report.AppendLine("[MISSING] " + warning);
        }

        if (errors.Count > 0)
        {
            Debug.LogError(report.ToString());
        }
        else if (warnings.Count > 0)
        {
            Debug.LogWarning(report.ToString());
        }
        else
        {
            Debug.Log(report.ToString());
        }
    }
}
