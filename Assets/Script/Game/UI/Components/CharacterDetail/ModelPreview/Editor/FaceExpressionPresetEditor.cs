using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FaceExpressionPreset))]
public sealed class FaceExpressionPresetEditor : Editor
{
    const float ZeroThreshold = 0.0001f;
    const float SmallValueThreshold = 0.01f;

    readonly List<CurveDiagnostic> unresolvedDiagnostics = new();

    SerializedProperty curvesProperty;

    void OnEnable()
    {
        curvesProperty = serializedObject.FindProperty("curves");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "curves");
        EditorGUILayout.Space(8f);
        DrawUnresolvedDiagnostics((FaceExpressionPreset)target);
        EditorGUILayout.Space(8f);
        EditorGUILayout.PropertyField(curvesProperty, true);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawUnresolvedDiagnostics(FaceExpressionPreset preset)
    {
        CollectUnresolvedDiagnostics(preset);

        EditorGUILayout.LabelField("未解析通道诊断", EditorStyles.boldLabel);
        if (unresolvedDiagnostics.Count == 0)
        {
            EditorGUILayout.HelpBox("当前 Preset 没有未解析通道。", MessageType.Info);
            return;
        }

        int activeCount = 0;
        int smallValueCount = 0;
        int zeroCount = 0;
        int invalidCount = 0;

        foreach (CurveDiagnostic diagnostic in unresolvedDiagnostics)
        {
            switch (diagnostic.State)
            {
                case DiagnosticState.Active:
                    activeCount++;
                    break;
                case DiagnosticState.SmallValue:
                    smallValueCount++;
                    break;
                case DiagnosticState.Zero:
                    zeroCount++;
                    break;
                case DiagnosticState.Invalid:
                    invalidCount++;
                    break;
            }
        }

        EditorGUILayout.HelpBox(
            $"共 {unresolvedDiagnostics.Count} 个：有效 {activeCount}，微小值 {smallValueCount}，零值 {zeroCount}，异常 {invalidCount}。",
            invalidCount > 0 ? MessageType.Error : MessageType.Warning);

        foreach (CurveDiagnostic diagnostic in unresolvedDiagnostics)
        {
            DrawDiagnostic(diagnostic);
        }
    }

    void CollectUnresolvedDiagnostics(FaceExpressionPreset preset)
    {
        unresolvedDiagnostics.Clear();
        if (preset.curves == null)
        {
            return;
        }

        foreach (FaceExpressionPreset.CurveData curveData in preset.curves)
        {
            if (curveData.bindingType != FaceCurveBindingType.Unresolved)
            {
                continue;
            }

            unresolvedDiagnostics.Add(CreateDiagnostic(curveData));
        }

        unresolvedDiagnostics.Sort((left, right) =>
        {
            int stateComparison = left.State.CompareTo(right.State);
            return stateComparison != 0
                ? stateComparison
                : left.ControllerIndex.CompareTo(right.ControllerIndex);
        });
    }

    static CurveDiagnostic CreateDiagnostic(FaceExpressionPreset.CurveData curveData)
    {
        Keyframe[] keys = curveData.curve?.keys ?? Array.Empty<Keyframe>();
        if (keys.Length == 0)
        {
            return new CurveDiagnostic(curveData, DiagnosticState.Zero, 0f, 0f, 0);
        }

        float minimum = float.PositiveInfinity;
        float maximum = float.NegativeInfinity;
        foreach (Keyframe key in keys)
        {
            if (float.IsNaN(key.value) || float.IsInfinity(key.value))
            {
                return new CurveDiagnostic(curveData, DiagnosticState.Invalid, 0f, 0f, keys.Length);
            }

            minimum = Mathf.Min(minimum, key.value);
            maximum = Mathf.Max(maximum, key.value);
        }

        float maximumAbsoluteValue = Mathf.Max(Mathf.Abs(minimum), Mathf.Abs(maximum));
        DiagnosticState state = maximumAbsoluteValue <= ZeroThreshold
            ? DiagnosticState.Zero
            : maximumAbsoluteValue <= SmallValueThreshold
                ? DiagnosticState.SmallValue
                : DiagnosticState.Active;

        return new CurveDiagnostic(curveData, state, minimum, maximum, keys.Length);
    }

    static void DrawDiagnostic(CurveDiagnostic diagnostic)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"Controller {diagnostic.ControllerIndex} / Channel {diagnostic.ChannelId}",
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(GetStateLabel(diagnostic.State), EditorStyles.miniBoldLabel);
            }

            string region = diagnostic.ControllerIndex <= 31 ? "Mouth" : "Eye / Brow";
            EditorGUILayout.LabelField("区域推测", region);
            EditorGUILayout.LabelField("关键帧数", diagnostic.KeyCount.ToString());
            EditorGUILayout.LabelField(
                "关键帧值范围",
                diagnostic.State == DiagnosticState.Invalid
                    ? "包含 NaN 或 Infinity"
                    : $"{diagnostic.Minimum:0.#####} ~ {diagnostic.Maximum:0.#####}");
        }
    }

    static string GetStateLabel(DiagnosticState state)
    {
        return state switch
        {
            DiagnosticState.Active => "有效数据",
            DiagnosticState.SmallValue => "可能为噪声",
            DiagnosticState.Zero => "无有效值",
            DiagnosticState.Invalid => "数据异常",
            _ => string.Empty
        };
    }

    enum DiagnosticState
    {
        Active,
        SmallValue,
        Zero,
        Invalid
    }

    readonly struct CurveDiagnostic
    {
        public readonly int ChannelId;
        public readonly int ControllerIndex;
        public readonly DiagnosticState State;
        public readonly float Minimum;
        public readonly float Maximum;
        public readonly int KeyCount;

        public CurveDiagnostic(
            FaceExpressionPreset.CurveData curveData,
            DiagnosticState state,
            float minimum,
            float maximum,
            int keyCount)
        {
            ChannelId = curveData.channelId;
            ControllerIndex = curveData.controllerIndex;
            State = state;
            Minimum = minimum;
            Maximum = maximum;
            KeyCount = keyCount;
        }
    }
}
