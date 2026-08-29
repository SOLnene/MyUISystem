using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class HumanoidAvatarGeneratorWindow : EditorWindow
{
    const string MenuPath = "Tools/Character Animation/Humanoid Avatar 生成工具";

    GameObject avatarRoot;
    Avatar sourceAvatar;

    [MenuItem(MenuPath)]
    static void Open()
    {
        HumanoidAvatarGeneratorWindow window =
            GetWindow<HumanoidAvatarGeneratorWindow>("Avatar 生成工具");
        window.minSize = new Vector2(430f, 220f);
        window.TryUseCurrentSelection();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "从指定根节点生成一份新的 Humanoid Avatar 资产。工具只读取当前层级和源 Avatar，" +
            "不会修改 FBX、Prefab、Animator 或 AnimationClip。",
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

        EditorGUILayout.Space(12f);
        using (new EditorGUI.DisabledScope(avatarRoot == null || sourceAvatar == null))
        {
            if (GUILayout.Button("生成新的 Humanoid Avatar", GUILayout.Height(32f)))
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
        string defaultName = $"{avatarRoot.name}_InnerRootAvatar";
        string outputPath = EditorUtility.SaveFilePanelInProject(
            "保存 Humanoid Avatar",
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
            Debug.Log($"[Avatar 生成工具] 已生成：{outputPath}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Avatar 生成工具] 生成失败：{exception.Message}");
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

            // 克隆对象只用于生成 Avatar，避免调整 T Pose 时污染场景或 Prefab 中的原对象。
            SceneManager.MoveGameObjectToScene(buildRoot, previewScene);
            buildRoot.transform.localPosition = sourceRoot.transform.localPosition;
            buildRoot.transform.localRotation = sourceRoot.transform.localRotation;
            buildRoot.transform.localScale = sourceRoot.transform.localScale;

            HumanDescription description = CreateRootedDescription(
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
                    "并且源 Avatar 的骨骼映射适用于该层级。");
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

    static HumanDescription CreateRootedDescription(
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

        foreach (HumanBone humanBone in sourceDescription.human)
        {
            if (!string.IsNullOrEmpty(humanBone.boneName) &&
                !targetByName.ContainsKey(humanBone.boneName))
            {
                throw new InvalidOperationException(
                    $"目标根节点下缺少源 Avatar 映射的骨骼：{humanBone.boneName}");
            }
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

        // 仅保留所选根及其后代，使新 Avatar 不再依赖原来的外层展示节点。
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
}
