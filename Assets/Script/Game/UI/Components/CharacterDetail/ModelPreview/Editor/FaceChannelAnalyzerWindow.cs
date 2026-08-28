using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class FaceChannelAnalyzerWindow : EditorWindow
{
    const string PresetFolder = "Assets/GameData/ModelView/FacePreset/Extracted";
    const float ZeroThreshold = 0.0001f;
    const float SmallValueThreshold = 0.01f;
    const float OneBaselineTolerance = 0.1f;
    const float SimilarityThreshold = 0.95f;
    const int SimilaritySampleCount = 16;

    readonly List<ChannelSummary> channels = new();
    readonly List<ChannelGroup> candidateGroups = new();

    Vector2 scrollPosition;
    ChannelSummary selectedChannel;
    string searchText = string.Empty;
    bool showInactiveChannels = true;
    bool showCandidateGroups = true;
    bool showChannelList = true;
    bool showDetails = true;

    [MenuItem("Tools/Character/Face Channel Analyzer")]
    static void OpenWindow()
    {
        GetWindow<FaceChannelAnalyzerWindow>("Face Channel Analyzer");
    }

    void OnEnable()
    {
        RefreshAnalysis();
    }

    void OnGUI()
    {
        DrawToolbar();

        if (channels.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"未在 {PresetFolder} 中找到未解析通道。",
                MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawSummary();
        DrawCandidateGroups();
        DrawChannelList();
        DrawSelectedChannel();
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(PresetFolder, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            searchText = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.Width(220f));
            if (GUILayout.Button("重新扫描", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                RefreshAnalysis();
            }
        }
    }

    void DrawSummary()
    {
        int activeCount = channels.Count(channel => channel.Pattern != ChannelPattern.Zero
                                                     && channel.Pattern != ChannelPattern.SmallValue);
        EditorGUILayout.HelpBox(
            $"唯一未解析通道 {channels.Count} 个，有效通道 {activeCount} 个，"
            + $"候选三元组 {candidateGroups.Count} 个。分析结果只读，不会修改 Preset。",
            MessageType.Info);

        showInactiveChannels = EditorGUILayout.ToggleLeft("显示零值和微小值通道", showInactiveChannels);
    }

    void DrawCandidateGroups()
    {
        showCandidateGroups = EditorGUILayout.Foldout(showCandidateGroups, "候选三元参数组", true);
        if (!showCandidateGroups)
        {
            return;
        }

        if (candidateGroups.Count == 0)
        {
            EditorGUILayout.HelpBox("没有发现连续且使用位置一致的三通道组。", MessageType.None);
            return;
        }

        foreach (ChannelGroup group in candidateGroups)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"Controller {group.First.ControllerIndex} ~ {group.Third.ControllerIndex}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField("结构推测", group.Description);
                EditorGUILayout.LabelField(
                    "通道",
                    $"{group.First.ChannelId}, {group.Second.ChannelId}, {group.Third.ChannelId}");
                EditorGUILayout.LabelField("共同 Preset", group.First.Occurrences.Count.ToString());

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("查看第一个通道"))
                    {
                        selectedChannel = group.First;
                        showDetails = true;
                    }

                    if (GUILayout.Button("查看数值最活跃的通道"))
                    {
                        selectedChannel = group.Channels
                            .OrderByDescending(channel => channel.MaximumAbsoluteValue)
                            .First();
                        showDetails = true;
                    }
                }
            }
        }
    }

    void DrawChannelList()
    {
        showChannelList = EditorGUILayout.Foldout(showChannelList, "未解析通道", true);
        if (!showChannelList)
        {
            return;
        }

        foreach (ChannelSummary channel in channels)
        {
            if (!MatchesFilter(channel))
            {
                continue;
            }

            string synchronizedChannel = channel.MostSimilarChannel != null
                                         && channel.Similarity >= SimilarityThreshold
                ? $" | 同步 Controller {channel.MostSimilarChannel.ControllerIndex} ({channel.Similarity:P0})"
                : string.Empty;
            string label = $"Controller {channel.ControllerIndex} / Channel {channel.ChannelId}"
                           + $" | {GetPatternLabel(channel.Pattern)}"
                           + $" | {channel.Minimum:0.#####} ~ {channel.Maximum:0.#####}"
                           + $" | Preset {channel.Occurrences.Count}"
                           + synchronizedChannel;

            GUIStyle style = ReferenceEquals(channel, selectedChannel)
                ? EditorStyles.miniButtonMid
                : EditorStyles.miniButton;
            if (GUILayout.Button(label, style, GUILayout.Height(24f)))
            {
                selectedChannel = channel;
                showDetails = true;
            }
        }
    }

    void DrawSelectedChannel()
    {
        if (selectedChannel == null)
        {
            return;
        }

        showDetails = EditorGUILayout.Foldout(
            showDetails,
            $"通道详情：Controller {selectedChannel.ControllerIndex} / Channel {selectedChannel.ChannelId}",
            true);
        if (!showDetails)
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("区域推测", selectedChannel.ControllerIndex <= 31 ? "Mouth" : "Eyes / Brows");
            EditorGUILayout.LabelField("数值特征", GetPatternLabel(selectedChannel.Pattern));
            EditorGUILayout.LabelField("全局范围", $"{selectedChannel.Minimum:0.#####} ~ {selectedChannel.Maximum:0.#####}");
            EditorGUILayout.LabelField("最大绝对值", selectedChannel.MaximumAbsoluteValue.ToString("0.#####"));
            EditorGUILayout.LabelField("出现次数", selectedChannel.Occurrences.Count.ToString());

            if (selectedChannel.MostSimilarChannel != null)
            {
                EditorGUILayout.LabelField(
                    "最相似通道",
                    $"Controller {selectedChannel.MostSimilarChannel.ControllerIndex} / "
                    + $"Channel {selectedChannel.MostSimilarChannel.ChannelId} ({selectedChannel.Similarity:P1})");
            }
        }

        foreach (ChannelOccurrence occurrence in selectedChannel.Occurrences
                     .OrderByDescending(item => item.MaximumAbsoluteValue))
        {
            DrawOccurrence(occurrence);
        }
    }

    static void DrawOccurrence(ChannelOccurrence occurrence)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(occurrence.Preset.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("定位资源", GUILayout.Width(76f)))
                {
                    Selection.activeObject = occurrence.Preset;
                    EditorGUIUtility.PingObject(occurrence.Preset);
                }
            }

            EditorGUILayout.LabelField("区域", occurrence.Preset.regions.ToString());
            EditorGUILayout.LabelField("关键帧范围", $"{occurrence.Minimum:0.#####} ~ {occurrence.Maximum:0.#####}");
            EditorGUILayout.LabelField("关键帧数", occurrence.Curve.length.ToString());
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.CurveField("曲线", occurrence.Curve);
            }
        }
    }

    bool MatchesFilter(ChannelSummary channel)
    {
        if (!showInactiveChannels
            && (channel.Pattern == ChannelPattern.Zero || channel.Pattern == ChannelPattern.SmallValue))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        string filter = searchText.Trim();
        return channel.ControllerIndex.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
               || channel.ChannelId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
               || channel.Occurrences.Any(occurrence =>
                   occurrence.Preset.name.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    void RefreshAnalysis()
    {
        (int controllerIndex, int channelId)? selectedKey = selectedChannel == null
            ? null
            : (selectedChannel.ControllerIndex, selectedChannel.ChannelId);

        channels.Clear();
        candidateGroups.Clear();

        Dictionary<(int controllerIndex, int channelId), ChannelSummary> summaries = new();
        string[] presetGuids = AssetDatabase.FindAssets("t:FaceExpressionPreset", new[] { PresetFolder });
        foreach (string presetGuid in presetGuids)
        {
            string presetPath = AssetDatabase.GUIDToAssetPath(presetGuid);
            FaceExpressionPreset preset = AssetDatabase.LoadAssetAtPath<FaceExpressionPreset>(presetPath);
            if (preset == null || preset.curves == null)
            {
                continue;
            }

            foreach (FaceExpressionPreset.CurveData curveData in preset.curves)
            {
                if (curveData.bindingType != FaceCurveBindingType.Unresolved)
                {
                    continue;
                }

                var key = (curveData.controllerIndex, curveData.channelId);
                if (!summaries.TryGetValue(key, out ChannelSummary summary))
                {
                    summary = new ChannelSummary(curveData.controllerIndex, curveData.channelId);
                    summaries.Add(key, summary);
                }

                summary.AddOccurrence(preset, presetPath, curveData.curve);
            }
        }

        channels.AddRange(summaries.Values.OrderBy(summary => summary.ControllerIndex));
        foreach (ChannelSummary channel in channels)
        {
            channel.CompleteStatistics();
        }

        FindSimilarChannels();
        BuildCandidateGroups();

        selectedChannel = selectedKey.HasValue
            ? channels.FirstOrDefault(channel => channel.ControllerIndex == selectedKey.Value.controllerIndex
                                                 && channel.ChannelId == selectedKey.Value.channelId)
            : channels.FirstOrDefault(channel => channel.Pattern != ChannelPattern.Zero
                                                 && channel.Pattern != ChannelPattern.SmallValue);
        Repaint();
    }

    void FindSimilarChannels()
    {
        foreach (ChannelSummary channel in channels)
        {
            float bestSimilarity = 0f;
            ChannelSummary bestMatch = null;
            foreach (ChannelSummary candidate in channels)
            {
                if (ReferenceEquals(channel, candidate)
                    || channel.OccurrenceSignature != candidate.OccurrenceSignature)
                {
                    continue;
                }

                float similarity = CalculateSimilarity(channel, candidate);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestMatch = candidate;
                }
            }

            channel.MostSimilarChannel = bestMatch;
            channel.Similarity = bestSimilarity;
        }
    }

    void BuildCandidateGroups()
    {
        int channelIndex = 0;
        while (channelIndex <= channels.Count - 3)
        {
            ChannelSummary first = channels[channelIndex];
            ChannelSummary second = channels[channelIndex + 1];
            ChannelSummary third = channels[channelIndex + 2];
            bool isConsecutive = second.ControllerIndex == first.ControllerIndex + 1
                                 && third.ControllerIndex == second.ControllerIndex + 1;
            bool hasSameOccurrences = first.OccurrenceSignature == second.OccurrenceSignature
                                      && second.OccurrenceSignature == third.OccurrenceSignature;
            if (!isConsecutive || !hasSameOccurrences)
            {
                channelIndex++;
                continue;
            }

            candidateGroups.Add(new ChannelGroup(first, second, third));
            channelIndex += 3;
        }
    }

    static float CalculateSimilarity(ChannelSummary left, ChannelSummary right)
    {
        Dictionary<string, ChannelOccurrence> rightOccurrences = right.Occurrences
            .ToDictionary(occurrence => occurrence.AssetPath, StringComparer.Ordinal);
        List<float> leftSamples = new();
        List<float> rightSamples = new();

        foreach (ChannelOccurrence leftOccurrence in left.Occurrences)
        {
            if (!rightOccurrences.TryGetValue(leftOccurrence.AssetPath, out ChannelOccurrence rightOccurrence))
            {
                continue;
            }

            // Different Presets have different durations, so correlation uses normalized time.
            for (int sampleIndex = 0; sampleIndex < SimilaritySampleCount; sampleIndex++)
            {
                float normalizedTime = SimilaritySampleCount == 1
                    ? 0f
                    : sampleIndex / (SimilaritySampleCount - 1f);
                leftSamples.Add(EvaluateNormalized(leftOccurrence.Curve, normalizedTime));
                rightSamples.Add(EvaluateNormalized(rightOccurrence.Curve, normalizedTime));
            }
        }

        if (leftSamples.Count == 0)
        {
            return 0f;
        }

        float leftAverage = leftSamples.Average();
        float rightAverage = rightSamples.Average();
        float numerator = 0f;
        float leftVariance = 0f;
        float rightVariance = 0f;
        for (int sampleIndex = 0; sampleIndex < leftSamples.Count; sampleIndex++)
        {
            float leftDelta = leftSamples[sampleIndex] - leftAverage;
            float rightDelta = rightSamples[sampleIndex] - rightAverage;
            numerator += leftDelta * rightDelta;
            leftVariance += leftDelta * leftDelta;
            rightVariance += rightDelta * rightDelta;
        }

        float denominator = Mathf.Sqrt(leftVariance * rightVariance);
        return denominator <= Mathf.Epsilon ? 0f : Mathf.Clamp01(numerator / denominator);
    }

    static float EvaluateNormalized(AnimationCurve curve, float normalizedTime)
    {
        if (curve == null || curve.length == 0)
        {
            return 0f;
        }

        float duration = curve.keys[^1].time;
        return curve.Evaluate(duration <= 0f ? 0f : normalizedTime * duration);
    }

    static string GetPatternLabel(ChannelPattern pattern)
    {
        return pattern switch
        {
            ChannelPattern.Zero => "恒定零",
            ChannelPattern.SmallValue => "微小值",
            ChannelPattern.ZeroBaseline => "零基准参数",
            ChannelPattern.OneBaseline => "一基准参数",
            ChannelPattern.Other => "其他参数",
            ChannelPattern.Invalid => "数据异常",
            _ => string.Empty
        };
    }

    enum ChannelPattern
    {
        Zero,
        SmallValue,
        ZeroBaseline,
        OneBaseline,
        Other,
        Invalid
    }

    sealed class ChannelSummary
    {
        public readonly int ControllerIndex;
        public readonly int ChannelId;
        public readonly List<ChannelOccurrence> Occurrences = new();

        public float Minimum { get; private set; }
        public float Maximum { get; private set; }
        public float MaximumAbsoluteValue { get; private set; }
        public ChannelPattern Pattern { get; private set; }
        public string OccurrenceSignature { get; private set; }
        public ChannelSummary MostSimilarChannel;
        public float Similarity;

        public ChannelSummary(int controllerIndex, int channelId)
        {
            ControllerIndex = controllerIndex;
            ChannelId = channelId;
        }

        public void AddOccurrence(FaceExpressionPreset preset, string assetPath, AnimationCurve curve)
        {
            Occurrences.Add(new ChannelOccurrence(preset, assetPath, curve));
        }

        public void CompleteStatistics()
        {
            Minimum = Occurrences.Min(occurrence => occurrence.Minimum);
            Maximum = Occurrences.Max(occurrence => occurrence.Maximum);
            MaximumAbsoluteValue = Mathf.Max(Mathf.Abs(Minimum), Mathf.Abs(Maximum));
            OccurrenceSignature = string.Join(
                "|",
                Occurrences.Select(occurrence => occurrence.AssetPath).OrderBy(path => path, StringComparer.Ordinal));

            if (Occurrences.Any(occurrence => occurrence.HasInvalidValue))
            {
                Pattern = ChannelPattern.Invalid;
                return;
            }

            if (MaximumAbsoluteValue <= ZeroThreshold)
            {
                Pattern = ChannelPattern.Zero;
                return;
            }

            if (MaximumAbsoluteValue <= SmallValueThreshold)
            {
                Pattern = ChannelPattern.SmallValue;
                return;
            }

            float averageFirstValue = Occurrences.Average(occurrence => occurrence.FirstValue);
            if (Mathf.Abs(averageFirstValue) <= SmallValueThreshold)
            {
                Pattern = ChannelPattern.ZeroBaseline;
            }
            else if (Mathf.Abs(averageFirstValue - 1f) <= OneBaselineTolerance)
            {
                Pattern = ChannelPattern.OneBaseline;
            }
            else
            {
                Pattern = ChannelPattern.Other;
            }
        }
    }

    sealed class ChannelOccurrence
    {
        public readonly FaceExpressionPreset Preset;
        public readonly string AssetPath;
        public readonly AnimationCurve Curve;
        public readonly float Minimum;
        public readonly float Maximum;
        public readonly float MaximumAbsoluteValue;
        public readonly float FirstValue;
        public readonly bool HasInvalidValue;

        public ChannelOccurrence(FaceExpressionPreset preset, string assetPath, AnimationCurve curve)
        {
            Preset = preset;
            AssetPath = assetPath;
            Curve = curve ?? new AnimationCurve();

            Keyframe[] keys = Curve.keys;
            if (keys.Length == 0)
            {
                Minimum = 0f;
                Maximum = 0f;
                FirstValue = 0f;
                return;
            }

            Minimum = float.PositiveInfinity;
            Maximum = float.NegativeInfinity;
            FirstValue = keys[0].value;
            foreach (Keyframe key in keys)
            {
                if (float.IsNaN(key.value) || float.IsInfinity(key.value))
                {
                    HasInvalidValue = true;
                    continue;
                }

                Minimum = Mathf.Min(Minimum, key.value);
                Maximum = Mathf.Max(Maximum, key.value);
            }

            if (float.IsInfinity(Minimum) || float.IsInfinity(Maximum))
            {
                Minimum = 0f;
                Maximum = 0f;
            }

            MaximumAbsoluteValue = Mathf.Max(Mathf.Abs(Minimum), Mathf.Abs(Maximum));
        }
    }

    sealed class ChannelGroup
    {
        public readonly ChannelSummary First;
        public readonly ChannelSummary Second;
        public readonly ChannelSummary Third;
        public readonly ChannelSummary[] Channels;
        public readonly string Description;

        public ChannelGroup(ChannelSummary first, ChannelSummary second, ChannelSummary third)
        {
            First = first;
            Second = second;
            Third = third;
            Channels = new[] { first, second, third };
            Description = GetDescription(Channels);
        }

        static string GetDescription(IReadOnlyCollection<ChannelSummary> channels)
        {
            if (channels.All(channel => channel.Pattern == ChannelPattern.OneBaseline))
            {
                return "一基准三元参数候选（可能是缩放或倍率）";
            }

            if (channels.All(channel => channel.Pattern == ChannelPattern.Zero
                                        || channel.Pattern == ChannelPattern.SmallValue
                                        || channel.Pattern == ChannelPattern.ZeroBaseline))
            {
                return "零基准三元参数候选（可能是位置、旋转或其他偏移）";
            }

            return "混合基准三元参数候选（需要结合原始控制器确认）";
        }
    }
}
