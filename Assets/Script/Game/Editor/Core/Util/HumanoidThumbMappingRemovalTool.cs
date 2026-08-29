using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class HumanoidThumbMappingRemovalTool : EditorWindow
{
    const string MenuPath =
        "Tools/Character Animation/Humanoid Avatar 拇指映射验证工具";

    static readonly HashSet<string> ThumbHumanBoneNames = CreateThumbHumanBoneNames();

    GameObject avatarRoot;
    Avatar sourceAvatar;

    [MenuItem(MenuPath)]
    static void Open()
    {
        HumanoidThumbMappingRemovalTool window =
            GetWindow<HumanoidThumbMappingRemovalTool>("拇指映射验证");
        window.minSize = new Vector2(460f, 250f);
        window.TryUseCurrentSelection();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "生成一份新的 Humanoid Avatar，仅移除左右拇指的六项 Human 映射。" +
            "拇指 Transform 仍保留在骨架中，可用于验证 Generic 动画曲线。" +
            "工具不会修改源 Avatar、FBX、Prefab 或 AnimationClip。",
            MessageType.Info);

        EditorGUILayout.Space(8f);
        avatarRoot = (GameObject)EditorGUILayout.ObjectField(
            "Avatar 根节点",
            avatarRoot,
            typeof(GameObject),
            true);
        sourceAvatar = (Avatar)EditorGUILayout.ObjectField(
            "源 Avatar",
            sourceAvatar,
            typeof(Avatar),
            false);

        EditorGUILayout.Space(6f);
        if (GUILayout.Button("使用当前选择"))
        {
            TryUseCurrentSelection();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("将移除的 Humanoid 映射", EditorStyles.boldLabel);
        foreach (string humanBoneName in ThumbHumanBoneNames)
        {
            EditorGUILayout.LabelField($"• {humanBoneName}");
        }

        EditorGUILayout.Space(12f);
        using (new EditorGUI.DisabledScope(avatarRoot == null || sourceAvatar == null))
        {
            if (GUILayout.Button("生成无拇指映射 Avatar", GUILayout.Height(32f)))
            {
                GenerateAvatarAsset();
            }
        }
    }

    void TryUseCurrentSelection()
    {
        GameObject selectedRoot = Selection.activeGameObject;
        if (selectedRoot == null)
        {
            return;
        }

        avatarRoot = selectedRoot;

        Animator sourceAnimator = selectedRoot.GetComponentInParent<Animator>(true);
        if (sourceAnimator != null && sourceAnimator.avatar != null)
        {
            sourceAvatar = sourceAnimator.avatar;
        }

        Repaint();
    }

    void GenerateAvatarAsset()
    {
        if (!TryValidateInputs(out string validationError))
        {
            EditorUtility.DisplayDialog("无法生成 Avatar", validationError, "确定");
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceAvatar);
        string defaultDirectory = string.IsNullOrEmpty(sourcePath)
            ? "Assets"
            : Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
        string defaultName = $"{avatarRoot.name}_GenericThumbsAvatar";
        string outputPath = EditorUtility.SaveFilePanelInProject(
            "保存无拇指映射 Avatar",
            defaultName,
            "asset",
            "请选择新 Avatar 的保存位置。原有资产不会被覆盖。",
            defaultDirectory);

        if (string.IsNullOrEmpty(outputPath))
        {
            return;
        }

        if (AssetDatabase.LoadMainAssetAtPath(outputPath) != null)
        {
            EditorUtility.DisplayDialog(
                "无法保存 Avatar",
                $"目标路径已经存在资产：\n{outputPath}\n\n工具不会覆盖现有资产。",
                "确定");
            return;
        }

        Avatar generatedAvatar = null;
        bool assetCreated = false;

        try
        {
            generatedAvatar = BuildAvatar(avatarRoot, sourceAvatar);
            generatedAvatar.name = Path.GetFileNameWithoutExtension(outputPath);

            AssetDatabase.CreateAsset(generatedAvatar, outputPath);
            AssetDatabase.SaveAssets();
            assetCreated = true;

            Selection.activeObject = generatedAvatar;
            EditorGUIUtility.PingObject(generatedAvatar);
            Debug.Log($"[拇指映射验证工具] 已生成：{outputPath}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[拇指映射验证工具] 生成失败：{exception.Message}");
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Avatar 生成失败", exception.Message, "确定");
        }
        finally
        {
            if (!assetCreated && generatedAvatar != null)
            {
                DestroyImmediate(generatedAvatar);
            }
        }
    }

    bool TryValidateInputs(out string error)
    {
        if (avatarRoot == null)
        {
            error = "请指定作为新 Avatar 根节点的 GameObject。";
            return false;
        }

        if (sourceAvatar == null)
        {
            error = "请指定用于复制 Humanoid 映射和 T Pose 的源 Avatar。";
            return false;
        }

        if (!sourceAvatar.isValid || !sourceAvatar.isHuman)
        {
            error = "源 Avatar 必须是有效的 Humanoid Avatar。";
            return false;
        }

        int mappedThumbCount = 0;
        foreach (HumanBone humanBone in sourceAvatar.humanDescription.human)
        {
            if (ThumbHumanBoneNames.Contains(humanBone.humanName))
            {
                mappedThumbCount++;
            }
        }

        if (mappedThumbCount != ThumbHumanBoneNames.Count)
        {
            error =
                $"源 Avatar 应包含 {ThumbHumanBoneNames.Count} 项拇指映射，" +
                $"但实际找到 {mappedThumbCount} 项。请确认没有重复处理，并且源 Avatar 映射完整。";
            return false;
        }

        error = null;
        return true;
    }

    static Avatar BuildAvatar(GameObject sourceRoot, Avatar mappingSource)
    {
        Scene previewScene = default;
        GameObject buildRoot = null;

        try
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            buildRoot = Instantiate(sourceRoot);
            buildRoot.name = sourceRoot.name;
            buildRoot.hideFlags = HideFlags.HideAndDontSave;

            // 构建过程只调整克隆体的 T Pose，避免污染场景或 Prefab 中的原对象。
            SceneManager.MoveGameObjectToScene(buildRoot, previewScene);
            buildRoot.transform.localPosition = sourceRoot.transform.localPosition;
            buildRoot.transform.localRotation = sourceRoot.transform.localRotation;
            buildRoot.transform.localScale = sourceRoot.transform.localScale;

            HumanDescription description = CreateDescription(
                buildRoot.transform,
                mappingSource.humanDescription);
            ApplySkeletonPose(buildRoot.transform, description.skeleton);

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(buildRoot, description);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                if (avatar != null)
                {
                    DestroyImmediate(avatar);
                }

                throw new InvalidOperationException(
                    "Unity 未能生成有效的 Humanoid Avatar。请确认所选根包含完整的人形骨架，" +
                    "并且源 Avatar 的映射适用于该层级。拇指属于可选骨骼，不应影响 Avatar 有效性。");
            }

            return avatar;
        }
        finally
        {
            if (buildRoot != null)
            {
                DestroyImmediate(buildRoot);
            }

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }
    }

    static HumanDescription CreateDescription(
        Transform targetRoot,
        HumanDescription sourceDescription)
    {
        SkeletonBone[] sourceSkeleton = sourceDescription.skeleton;
        if (sourceSkeleton == null || sourceSkeleton.Length == 0)
        {
            throw new InvalidOperationException("源 Avatar 不包含可复用的 T Pose 骨架数据。");
        }

        var sourcePoseByName = new Dictionary<string, SkeletonBone>(sourceSkeleton.Length);
        foreach (SkeletonBone bone in sourceSkeleton)
        {
            if (!sourcePoseByName.TryAdd(bone.name, bone))
            {
                throw new InvalidOperationException(
                    $"源 Avatar 的骨架中存在重名节点：{bone.name}。无法安全重建绑定根。");
            }
        }

        List<Transform> targetTransforms = GetHierarchyTransforms(targetRoot);
        var targetByName = new Dictionary<string, Transform>(targetTransforms.Count);
        foreach (Transform targetTransform in targetTransforms)
        {
            if (!targetByName.TryAdd(targetTransform.name, targetTransform))
            {
                throw new InvalidOperationException(
                    $"目标层级中存在重名节点：{targetTransform.name}。Humanoid 映射无法可靠区分它们。");
            }
        }

        var retainedHumanBones = new List<HumanBone>(sourceDescription.human.Length);
        foreach (HumanBone humanBone in sourceDescription.human)
        {
            if (ThumbHumanBoneNames.Contains(humanBone.humanName))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(humanBone.boneName) &&
                !targetByName.ContainsKey(humanBone.boneName))
            {
                throw new InvalidOperationException(
                    $"目标根节点下缺少源 Avatar 映射的骨骼：{humanBone.boneName}");
            }

            retainedHumanBones.Add(humanBone);
        }

        var rootedSkeleton = new List<SkeletonBone>(targetTransforms.Count);
        foreach (Transform targetTransform in targetTransforms)
        {
            if (sourcePoseByName.TryGetValue(targetTransform.name, out SkeletonBone pose))
            {
                rootedSkeleton.Add(pose);
            }
        }

        if (rootedSkeleton.Count == 0 || rootedSkeleton[0].name != targetRoot.name)
        {
            throw new InvalidOperationException(
                $"源 Avatar 的骨架描述中没有所选根节点：{targetRoot.name}");
        }

        // 只移除 Human 映射，保留拇指 Transform 的骨架描述供 Generic 曲线绑定。
        sourceDescription.human = retainedHumanBones.ToArray();
        sourceDescription.skeleton = rootedSkeleton.ToArray();
        return sourceDescription;
    }

    static void ApplySkeletonPose(Transform root, IReadOnlyList<SkeletonBone> skeleton)
    {
        var poseByName = new Dictionary<string, SkeletonBone>(skeleton.Count);
        foreach (SkeletonBone bone in skeleton)
        {
            poseByName.Add(bone.name, bone);
        }

        foreach (Transform target in GetHierarchyTransforms(root))
        {
            if (!poseByName.TryGetValue(target.name, out SkeletonBone pose))
            {
                continue;
            }

            target.localPosition = pose.position;
            target.localRotation = pose.rotation;
            target.localScale = pose.scale;
        }
    }

    static List<Transform> GetHierarchyTransforms(Transform root)
    {
        var results = new List<Transform>();
        AddHierarchyTransforms(root, results);
        return results;
    }

    static void AddHierarchyTransforms(Transform current, List<Transform> results)
    {
        results.Add(current);
        for (int childIndex = 0; childIndex < current.childCount; childIndex++)
        {
            AddHierarchyTransforms(current.GetChild(childIndex), results);
        }
    }

    static HashSet<string> CreateThumbHumanBoneNames()
    {
        HumanBodyBones[] thumbBones =
        {
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal
        };

        var names = new HashSet<string>();
        foreach (HumanBodyBones thumbBone in thumbBones)
        {
            names.Add(HumanTrait.BoneName[(int)thumbBone]);
        }

        return names;
    }
}
