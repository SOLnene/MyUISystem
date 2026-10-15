using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class GachaScheduleWindow : EditorWindow
{
    [SerializeField] GachaScheduleDocument document;
    [SerializeField] bool draftChanged;
    Vector2 scroll;
    readonly Dictionary<string, string> poolNames = new Dictionary<string, string>(StringComparer.Ordinal);
    readonly Dictionary<string, string> poolKinds = new Dictionary<string, string>(StringComparer.Ordinal);
    string[] candidates = Array.Empty<string>();
    int selectedCandidate;
    string feedback;
    MessageType feedbackType;
    GUIStyle headingStyle;

    static string SchedulePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "CloudSaveServer", "gacha-schedule.json"));

    [MenuItem("Tools/MyUISystem/服务器/卡池排期管理", false, 400)]
    static void Open()
    {
        var window = GetWindow<GachaScheduleWindow>("卡池排期管理");
        window.minSize = new Vector2(760, 520);
        window.Show();
    }

    void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
        if (document == null) Reload();
        else RefreshCatalog();
    }

    void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
    void OnInspectorUpdate() => Repaint();
    void OnUndoRedo() { RefreshCatalog(); Repaint(); }

    void OnGUI()
    {
        headingStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("卡池排期管理" + (draftChanged ? "  ·  未保存" : ""), headingStyle);
        EditorGUILayout.LabelField(SchedulePath, EditorStyles.miniLabel);
        EditorGUILayout.HelpBox("勾选启用并设置北京时间。开始时刻包含在开放区间内，结束时刻不包含；时间留空表示不限。保存后，账号模式重新打开抽卡页即可获取新排期。", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("重新读取", GUILayout.Width(100)))
            {
                if (!draftChanged || EditorUtility.DisplayDialog("重新读取排期", "将放弃窗口内尚未保存的修改。", "重新读取", "返回"))
                    Reload();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("打开配置目录", GUILayout.Width(120)))
                EditorUtility.RevealInFinder(SchedulePath);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(document == null || !draftChanged))
            {
                if (GUILayout.Button("保存排期", GUILayout.Width(120), GUILayout.Height(26)))
                {
                    SaveDraft();
                    GUIUtility.ExitGUI();
                }
            }
        }
        if (!string.IsNullOrEmpty(feedback)) EditorGUILayout.HelpBox(feedback, feedbackType);
        if (document == null) return;

        var now = DateTimeOffset.UtcNow;
        EditorGUILayout.LabelField("已保存版本：" + document.Version + "    北京时间：" +
            now.ToOffset(TimeSpan.FromHours(8)).ToString(GachaScheduleDocument.BeijingTimeFormat, CultureInfo.InvariantCulture));
        var active = document.Pools.Where(pool => pool.Status(now) == "开放中").Select(pool => DisplayName(pool.Key)).ToArray();
        EditorGUILayout.HelpBox("当前草稿开放预览（" + active.Length + " 个）：" +
            (active.Length == 0 ? "无" : string.Join(" → ", active)) + (draftChanged ? "\n此预览尚未保存到服务器。" : ""), MessageType.None);
        EditorGUILayout.LabelField("时间格式：2026-10-14 18:00:00    ·    列表顺序就是客户端显示顺序", EditorStyles.miniLabel);

        int moveFrom = -1, moveTo = -1;
        using (var view = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = view.scrollPosition;
            for (int index = 0; index < document.Pools.Count; index++)
            {
                var pool = document.Pools[index];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool enabled = EditorGUILayout.ToggleLeft((index + 1) + ". " + DisplayName(pool.Key), pool.Enabled, EditorStyles.boldLabel);
                        if (enabled != pool.Enabled)
                        {
                            Undo.RecordObject(this, "启用卡池排期");
                            pool.Enabled = enabled;
                            MarkDraftChanged();
                        }
                        GUILayout.Label(pool.Status(now), GUILayout.Width(100));
                        using (new EditorGUI.DisabledScope(index == 0))
                            if (GUILayout.Button("上移", GUILayout.Width(48))) { moveFrom = index; moveTo = index - 1; }
                        using (new EditorGUI.DisabledScope(index == document.Pools.Count - 1))
                            if (GUILayout.Button("下移", GUILayout.Width(48))) { moveFrom = index; moveTo = index + 1; }
                    }
                    string kind = poolKinds.TryGetValue(pool.Key, out var value) ? value : "配置未读取";
                    EditorGUILayout.LabelField(pool.Key + "    ·    " + kind, EditorStyles.miniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("开始（北京）", GUILayout.Width(82));
                        string start = EditorGUILayout.TextField(pool.StartsAtBeijing);
                        GUILayout.Label("结束（北京）", GUILayout.Width(82));
                        string end = EditorGUILayout.TextField(pool.EndsAtBeijing);
                        if (start != pool.StartsAtBeijing || end != pool.EndsAtBeijing)
                        {
                            Undo.RecordObject(this, "修改卡池排期时间");
                            pool.StartsAtBeijing = start;
                            pool.EndsAtBeijing = end;
                            MarkDraftChanged();
                        }
                    }
                    if (pool.Status(now).Contains("无效"))
                        EditorGUILayout.HelpBox("请检查时间格式，结束时间必须晚于开始时间。留空表示不限。", MessageType.Error);
                }
            }
        }
        // Reorder after drawing the list; changing its indices mid-layout would bind later controls to the wrong pool.
        if (moveFrom >= 0)
        {
            Undo.RecordObject(this, "调整卡池排期顺序");
            var pool = document.Pools[moveFrom];
            document.Pools.RemoveAt(moveFrom);
            document.Pools.Insert(moveTo, pool);
            MarkDraftChanged();
            GUIUtility.ExitGUI();
        }
        DrawCandidates();
    }

    void DrawCandidates()
    {
        if (candidates.Length == 0) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            selectedCandidate = EditorGUILayout.Popup("未加入排期的配置", selectedCandidate,
                candidates.Select(key => DisplayName(key) + " (" + key + ")").ToArray());
            using (new EditorGUI.DisabledScope(document.Pools.Count >= 64))
            {
                if (GUILayout.Button("加入（默认关闭）", GUILayout.Width(130)))
                {
                    Undo.RecordObject(this, "加入卡池排期");
                    document.Pools.Add(new GachaScheduleDocument.Slot { Key = candidates[selectedCandidate], Enabled = false });
                    MarkDraftChanged();
                    RefreshCatalog();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    void MarkDraftChanged()
    {
        draftChanged = true;
        feedback = null;
    }

    string DisplayName(string key) => poolNames.TryGetValue(key, out var name) ? name : key;

    void Reload()
    {
        try
        {
            var loaded = GachaScheduleDocument.Load(SchedulePath);
            document = loaded;
            draftChanged = false;
            Undo.ClearUndo(this);
            RefreshCatalog();
            feedback = "已读取服务器排期；配置文件存在不会自动启用卡池。";
            feedbackType = MessageType.Info;
        }
        catch (Exception error)
        {
            feedback = "读取失败，原文件未修改：" + error.Message;
            feedbackType = MessageType.Error;
        }
    }

    void SaveDraft()
    {
        try
        {
            string backup = document.Save();
            draftChanged = false;
            Undo.ClearUndo(this);
            feedback = "已保存版本 " + document.Version + "。旧排期备份：" + backup +
                "\n服务器无需重启；账号模式重新打开抽卡页获取。本工具只更新本机服务器配置。";
            feedbackType = MessageType.Info;
        }
        catch (Exception error)
        {
            feedback = "保存失败：" + error.Message;
            feedbackType = MessageType.Error;
        }
    }

    void RefreshCatalog()
    {
        poolNames.Clear();
        poolKinds.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:GachaPoolUIConfig", new[] { "Assets/GameData" }))
        {
            var config = AssetDatabase.LoadAssetAtPath<GachaPoolUIConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null || string.IsNullOrEmpty(config.gachaKey)) continue;
            poolNames[config.gachaKey] = string.IsNullOrEmpty(config.primaryRateUpName) ? config.name : config.primaryRateUpName;
        }
        string root = Path.GetDirectoryName(SchedulePath);
        if (!Directory.Exists(root)) { candidates = Array.Empty<string>(); return; }
        var keys = new List<string>();
        foreach (string path in Directory.GetFiles(root, "gacha-*.json"))
        {
            string key = Path.GetFileNameWithoutExtension(path).Substring("gacha-".Length);
            if (key == "schedule" || key.StartsWith("common-", StringComparison.Ordinal)) continue;
            try { poolKinds[key] = GachaScheduleDocument.ReadPoolKind(root, key); keys.Add(key); }
            catch (Exception error) { poolKinds[key] = "配置错误：" + error.Message; }
        }
        var scheduled = new HashSet<string>(document?.Pools.Select(pool => pool.Key) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        candidates = keys.Where(key => !scheduled.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        selectedCandidate = 0;
    }
}
