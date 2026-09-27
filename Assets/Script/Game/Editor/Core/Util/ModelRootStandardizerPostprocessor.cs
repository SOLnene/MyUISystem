using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class ModelRootStandardizerPostprocessor : AssetPostprocessor
{
    internal const string RootName = "ModelRoot";
    const string Marker = "CharacterAnimation.ModelRoot.v1=";

    [Serializable]
    internal sealed class Settings
    {
        public string sourceNode;
    }

    internal static Settings ReadSettings(AssetImporter importer)
    {
        foreach (string line in (importer.userData ?? "").Split('\n'))
        {
            if (line.StartsWith(Marker, StringComparison.Ordinal))
            {
                Settings settings = JsonUtility.FromJson<Settings>(line.Substring(Marker.Length));
                if (settings == null || string.IsNullOrEmpty(settings.sourceNode) ||
                    settings.sourceNode.Split('/').Any(string.IsNullOrEmpty))
                    throw new InvalidOperationException("模型标准化配置损坏，请检查副本的导入配置。");
                return settings;
            }
        }
        return null;
    }

    internal static void Enable(ModelImporter importer, string sourceNode)
    {
        if (ReadSettings(importer) != null)
            throw new InvalidOperationException("此资产已经启用标准命名，请选择未转换的源 FBX。");
        // 用独立行追加配置，保留其他插件已有的 userData。
        importer.userData = (importer.userData ?? "") + "\n" + Marker +
            JsonUtility.ToJson(new Settings { sourceNode = sourceNode });
    }

    internal static SerializedProperty Field(SerializedProperty property, string name)
    {
        SerializedProperty result = property.FindPropertyRelative(name) ??
            property.FindPropertyRelative("m_" + char.ToUpperInvariant(name[0]) + name.Substring(1));
        return result ?? throw new InvalidOperationException($"当前 Unity 不提供预期序列化字段：{property.propertyPath}.{name}");
    }

    internal static void SynchronizeDescription(ModelImporter importer, string oldName, bool apply)
    {
        if (importer.animationType != ModelImporterAnimationType.Human ||
            importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            throw new InvalidOperationException("初版只支持 Humanoid / Create From This Model。");

        if (oldName.Contains("/"))
        {
            string[] wrapperNames = oldName.Split('/');
            foreach (HumanBone bone in importer.humanDescription.human)
                if (wrapperNames.Contains(bone.boneName) || bone.boneName == RootName)
                    throw new InvalidOperationException("包装层属于 Humanoid 映射，不能合并：" + bone.boneName);
            return;
        }

        foreach (HumanBone bone in importer.humanDescription.human)
            if (bone.boneName == oldName || bone.boneName == RootName)
                throw new InvalidOperationException("待改名节点属于 Humanoid 映射，不能作为包装根处理。");

        using var serialized = new SerializedObject(importer);
        SerializedProperty skeleton = serialized.FindProperty("m_HumanDescription.m_Skeleton");
        if (skeleton == null || !skeleton.isArray || skeleton.arraySize < 2)
            throw new InvalidOperationException("导入配置缺少已保存的参考姿态骨架；工具不会自动猜测 T Pose。");

        string outerName = Field(skeleton.GetArrayElementAtIndex(0), "name").stringValue;
        int innerIndex = -1;
        for (int i = 1; i < skeleton.arraySize; i++)
        {
            SerializedProperty bone = skeleton.GetArrayElementAtIndex(i);
            string name = Field(bone, "name").stringValue;
            if (name != oldName && name != RootName) continue;
            if (innerIndex >= 0 || Field(bone, "parentName").stringValue != outerName)
                throw new InvalidOperationException("骨架描述中的中间根不唯一，或不是外层根的直接子节点。");
            innerIndex = i;
        }
        if (innerIndex < 0)
            throw new InvalidOperationException($"骨架描述中找不到中间根：{oldName}");

        // 同名外层根可能仍被其他节点引用。无法区分归属时拒绝改名，避免全局替换误伤。
        if (outerName == oldName)
            throw new InvalidOperationException("骨架描述的内外层完全同名，parentName 无法唯一确定父节点；本工具不会猜测归属。");

        Field(skeleton.GetArrayElementAtIndex(innerIndex), "name").stringValue = RootName;
        for (int i = 1; i < skeleton.arraySize; i++)
        {
            if (i == innerIndex) continue;
            SerializedProperty parent = Field(skeleton.GetArrayElementAtIndex(i), "parentName");
            if (parent.stringValue == oldName) parent.stringValue = RootName;
        }

        // 只编辑名称字段，不通过重新构造 HumanDescription 丢失隐藏的导入参数。
        if (apply) serialized.ApplyModifiedPropertiesWithoutUndo();
        if (apply)
        {
            string[] exposed = importer.extraExposedTransformPaths;
            for (int i = 0; i < exposed.Length; i++)
                exposed[i] = RenamePath(exposed[i], oldName);
            importer.extraExposedTransformPaths = exposed;
        }
    }

    static string RenamePath(string path, string oldName)
    {
        if (path == oldName) return RootName;
        return path.StartsWith(oldName + "/", StringComparison.Ordinal)
            ? RootName + path.Substring(oldName.Length) : path;
    }

    internal static Transform[] ValidateWrapperChain(Transform root, string path)
    {
        string[] names = path.Split('/');
        Transform inner = root.Find(path);
        // 导入回调有时不包含 Unity 生成的最外层资产根，此时路径首段就是回调根本身。
        if (inner == null && root.name == names[0])
            inner = root.Find(string.Join("/", names.Skip(1)));
        if (inner == null || inner.Find("Bip001") == null)
            throw new InvalidOperationException("找不到直接包含 Bip001 的模型根：" + path);
        var chain = new Transform[names.Length];
        for (int i = chain.Length - 1; i >= 0; i--)
        {
            if (inner == null || inner.name != names[i])
                throw new InvalidOperationException("包装层路径与导入层级不一致：" + path);
            chain[i] = inner;
            if ((i < chain.Length - 1 && inner.childCount != 1) ||
                inner.GetComponents<Component>().Any(component => component is not Transform))
                throw new InvalidOperationException("包装层包含额外分支或组件，不能自动移除：" + names[i]);
            inner = inner.parent;
        }
        if (chain[0] != root && (chain[0].parent != root || root.childCount != 1))
            throw new InvalidOperationException("模型根存在其他并列节点，不能安全合并为唯一 ModelRoot。");
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (chain.Contains(renderer.rootBone) || renderer.bones.Any(chain.Contains))
                throw new InvalidOperationException("包装层被蒙皮引用，不能自动移除：" + renderer.name);
        return chain;
    }

    static void AppendPose(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale,
        Vector3 nextPosition, Quaternion nextRotation, Vector3 nextScale)
    {
        Matrix4x4 expected = Matrix4x4.TRS(position, rotation, scale) *
            Matrix4x4.TRS(nextPosition, nextRotation, nextScale);
        position += rotation * Vector3.Scale(scale, nextPosition);
        rotation *= nextRotation;
        scale = Vector3.Scale(scale, nextScale);
        Matrix4x4 actual = Matrix4x4.TRS(position, rotation, scale);
        for (int i = 0; i < 16; i++)
            if (Mathf.Abs(expected[i] - actual[i]) > 0.00001f)
                throw new InvalidOperationException("包装层变换包含无法无损合并的缩放/旋转组合。");
    }

    static void CollapseWrapperChain(GameObject root, string path)
    {
        Transform[] chain = ValidateWrapperChain(root.transform, path);
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one;
        foreach (Transform node in chain)
            AppendPose(ref position, ref rotation, ref scale, node.localPosition, node.localRotation, node.localScale);
        Transform top = chain[0];
        Transform inner = chain[chain.Length - 1];
        // 合并包装层自身的变换，实际骨骼/网格保持原 local TRS，辅助动画仍使用同一坐标系。
        top.SetLocalPositionAndRotation(position, rotation);
        top.localScale = scale;
        while (inner.childCount > 0) inner.GetChild(0).SetParent(top, false);
        UnityEngine.Object.DestroyImmediate(chain[1].gameObject);
        top.name = RootName;
    }

    void OnPreprocessModel()
    {
        Settings settings = ReadSettings(assetImporter);
        if (settings != null)
            SynchronizeDescription((ModelImporter)assetImporter, settings.sourceNode, true);
    }

    void OnPostprocessMeshHierarchy(GameObject root)
    {
        Settings settings = ReadSettings(assetImporter);
        if (settings == null) return;

        if (settings.sourceNode.Contains("/"))
        {
            CollapseWrapperChain(root, settings.sourceNode);
            return;
        }

        Transform target = root.transform.Find(settings.sourceNode);
        // 此回调可能收到源 FBX 根，而不是 Unity 后续添加的资产包装根。
        if (target == null && root.name == settings.sourceNode) target = root.transform;
        if (target == null)
            throw new InvalidOperationException($"导入层级找不到待改名节点：{settings.sourceNode}");
        if (target != root.transform && root.transform.Find(RootName) != null)
            throw new InvalidOperationException("导入层级已存在 ModelRoot，无法安全改名。");
        target.name = RootName;
    }
}
