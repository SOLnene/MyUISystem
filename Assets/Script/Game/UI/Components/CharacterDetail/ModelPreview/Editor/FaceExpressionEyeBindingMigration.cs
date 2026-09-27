using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class FaceExpressionEyeBindingMigration : EditorWindow
{
    const string MenuPath = "Tools/MyUISystem/角色展示/表情/眼睛通道映射迁移";

    static readonly MigrationRule[] Rules =
    {
        new(44, 4028, "Eye_Default", "Eye_WinkA_L"),
        new(45, 4029, "Eye_WinkA_L", "Eye_WinkA_R"),
        new(46, 4085, "Eye_WinkA_R", "Eye_Default")
    };

    [SerializeField] DefaultAsset targetFolder;

    readonly List<PresetChange> changes = new();
    Vector2 scrollPosition;
    string statusMessage;
    MessageType statusType = MessageType.Info;

    [MenuItem(MenuPath, false, 353)]
    static void ShowWindow()
    {
        FaceExpressionEyeBindingMigration window = GetWindow<FaceExpressionEyeBindingMigration>();
        window.titleContent = new GUIContent("眼睛通道迁移");
        window.minSize = new Vector2(640f, 420f);
        window.TryUseSelectedFolder();
        window.RefreshChanges();
        window.Show();
    }

    void OnEnable()
    {
        if (targetFolder == null)
        {
            TryUseSelectedFolder();
        }

        RefreshChanges();
    }

    void OnProjectChange()
    {
        RefreshChanges();
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("眼睛通道映射迁移", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("选择一个表情资源文件夹，工具会递归扫描其子目录，并且只修改三组已确认的旧眼睛绑定。", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("目标文件夹", targetFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            statusMessage = string.Empty;
            RefreshChanges();
        }

        string folderPath = GetTargetFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            EditorGUILayout.HelpBox("请将 Project 窗口中的目标文件夹拖到上方。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("扫描路径", folderPath);
        EditorGUILayout.LabelField("待迁移资源", changes.Count.ToString());
        EditorGUILayout.LabelField("待迁移绑定", changes.Sum(change => change.BindingCount).ToString());

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("资源清单", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, EditorStyles.helpBox);
        foreach (PresetChange change in changes)
        {
            EditorGUILayout.LabelField($"{change.Path}（{change.BindingCount}）");
        }
        EditorGUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        using (new EditorGUI.DisabledGroupScope(changes.Count == 0))
        {
            if (GUILayout.Button("执行迁移", GUILayout.Height(32f)))
            {
                ApplyChanges(folderPath);
            }
        }
    }

    bool TryUseSelectedFolder()
    {
        string selectedFolderPath = Selection.assetGUIDs
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(AssetDatabase.IsValidFolder);
        if (string.IsNullOrEmpty(selectedFolderPath))
        {
            return false;
        }

        targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedFolderPath);
        return targetFolder != null;
    }

    string GetTargetFolderPath()
    {
        string folderPath = AssetDatabase.GetAssetPath(targetFolder);
        return AssetDatabase.IsValidFolder(folderPath) ? folderPath : string.Empty;
    }

    void RefreshChanges()
    {
        changes.Clear();
        string folderPath = GetTargetFolderPath();
        if (!string.IsNullOrEmpty(folderPath))
        {
            changes.AddRange(CollectChanges(folderPath));
        }
    }

    void ApplyChanges(string folderPath)
    {
        List<PresetChange> changesToApply = changes.ToList();
        if (changesToApply.Count == 0)
        {
            return;
        }

        int bindingCount = changesToApply.Sum(change => change.BindingCount);
        if (!EditorUtility.DisplayDialog(
                "执行眼睛通道映射迁移",
                $"将更新 {changesToApply.Count} 个表情资源，共 {bindingCount} 条绑定。\n\n只修改三组已确认的旧绑定名称，不修改动画曲线、嘴部绑定、GUID 或 Meta。",
                "执行",
                "取消"))
        {
            return;
        }

        UnityEngine.Object[] targets = changesToApply
            .Select(change => (UnityEngine.Object)change.Preset)
            .ToArray();
        Undo.RecordObjects(targets, "迁移表情眼睛通道映射");

        foreach (PresetChange change in changesToApply)
        {
            foreach (FaceExpressionPreset.CurveData curve in change.Preset.curves)
            {
                // 同时匹配通道、控制器和旧名称，避免覆盖已经人工校正或忽略的绑定。
                MigrationRule rule = Rules.FirstOrDefault(candidate => candidate.Matches(curve));
                if (rule != null)
                {
                    curve.blendShapeName = rule.NewName;
                }
            }

            EditorUtility.SetDirty(change.Preset);
        }

        AssetDatabase.SaveAssets();
        RefreshChanges();

        if (changes.Count > 0)
        {
            statusMessage = $"迁移后仍有 {changes.Count} 个资源包含旧绑定，请检查 Console。";
            statusType = MessageType.Error;
            Debug.LogError($"眼睛通道映射迁移后仍有 {changes.Count} 个资源包含旧绑定：{folderPath}");
            return;
        }

        statusMessage = $"已更新 {changesToApply.Count} 个表情资源，共 {bindingCount} 条绑定。";
        statusType = MessageType.Info;
        Debug.Log($"眼睛通道映射迁移完成：{statusMessage} 路径：{folderPath}");
    }

    static List<PresetChange> CollectChanges(string folderPath)
    {
        List<PresetChange> changes = new();
        IEnumerable<string> presetGuids = AssetDatabase
            .FindAssets("t:FaceExpressionPreset", new[] { folderPath })
            .Distinct();

        foreach (string presetGuid in presetGuids)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuid);
            FaceExpressionPreset preset = AssetDatabase.LoadAssetAtPath<FaceExpressionPreset>(presetPath);
            if (preset == null || preset.curves == null)
            {
                continue;
            }

            int bindingCount = preset.curves.Count(curve => Rules.Any(rule => rule.Matches(curve)));
            if (bindingCount > 0)
            {
                changes.Add(new PresetChange(preset, presetPath, bindingCount));
            }
        }

        return changes.OrderBy(change => change.Path).ToList();
    }

    sealed class MigrationRule
    {
        public readonly int ControllerIndex;
        public readonly int ChannelId;
        public readonly string OldName;
        public readonly string NewName;

        public MigrationRule(int controllerIndex, int channelId, string oldName, string newName)
        {
            ControllerIndex = controllerIndex;
            ChannelId = channelId;
            OldName = oldName;
            NewName = newName;
        }

        public bool Matches(FaceExpressionPreset.CurveData curve)
        {
            return curve.controllerIndex == ControllerIndex
                   && curve.channelId == ChannelId
                   && curve.bindingType == FaceCurveBindingType.BlendShape
                   && curve.blendShapeName == OldName;
        }
    }

    sealed class PresetChange
    {
        public readonly FaceExpressionPreset Preset;
        public readonly string Path;
        public readonly int BindingCount;

        public PresetChange(FaceExpressionPreset preset, string path, int bindingCount)
        {
            Preset = preset;
            Path = path;
            BindingCount = bindingCount;
        }
    }
}
