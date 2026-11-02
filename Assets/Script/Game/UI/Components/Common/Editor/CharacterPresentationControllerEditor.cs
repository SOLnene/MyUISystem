using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterPresentationController))]
public sealed class CharacterPresentationControllerEditor : Editor
{
    SerializedProperty transitionSpeed;
    SerializedProperty timedBlendTotal;
    SerializedProperty timedBlendEnter;
    SerializedProperty timedBlendLoop;
    SerializedProperty timedBlendAsToBs;
    SerializedProperty asToLoopPlay;
    SerializedProperty bsToLoopPlay;
    float timedBlendTotalDuration => timedBlendTotal.floatValue;
    float timedBlendEnterDuration => timedBlendEnter.floatValue;
    float timedBlendLoopDuration => timedBlendLoop.floatValue;
    void OnEnable()
    {
        transitionSpeed = serializedObject.FindProperty("transitionSpeed");
        timedBlendTotal = serializedObject.FindProperty("timedBlendTotalDuration");
        timedBlendEnter = serializedObject.FindProperty("timedBlendEnterDuration");
        timedBlendLoop = serializedObject.FindProperty("timedBlendLoopDuration");
        timedBlendAsToBs = serializedObject.FindProperty("timedBlendAsToBsDuration");
        asToLoopPlay = serializedObject.FindProperty("asToLoopPlayDuration");
        bsToLoopPlay = serializedObject.FindProperty("bsToLoopPlayDuration");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("动画切换设置", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(transitionSpeed, new GUIContent(
                "切换过程倍速", "1 为原速，2 为两倍速。仅加速角色动作、表情、混合和武器显隐；手动配置保留原值，稳定展示 Loop 恢复原速。镜头与界面面板保持原速度。修改从下一次切换生效。"));
            if (!(transitionSpeed.floatValue > 0f) || float.IsInfinity(transitionSpeed.floatValue))
                EditorGUILayout.HelpBox("切换过程倍速必须是有限正数；无效值按 1 倍处理。", MessageType.Error);
            timedBlendTotal.floatValue = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("特殊互切总时长", timedBlendTotalDuration));
            timedBlendEnter.floatValue = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("进入 BS／AS 混合时间", timedBlendEnterDuration));
            timedBlendAsToBs.floatValue = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("AS→BS 混合时长", timedBlendAsToBs.floatValue));
            timedBlendLoop.floatValue = Mathf.Max(
                0f,
                EditorGUILayout.FloatField("接入 Loop 混合时间", timedBlendLoopDuration));
            asToLoopPlay.floatValue = Mathf.Max(-1f, EditorGUILayout.FloatField(
                new GUIContent("AS 混合前播放时间", "单位：秒。从 AS 片段起点计时，包含入口混合；负数沿用自然时长。仅作用于返回 Default。"), asToLoopPlay.floatValue));
            bsToLoopPlay.floatValue = Mathf.Max(-1f, EditorGUILayout.FloatField(
                new GUIContent("BS 混合前播放时间", "单位：秒。从 BS 片段起点计时，包含入口混合；负数沿用自然时长。仅作用于从 Default 进入展示。"), bsToLoopPlay.floatValue));
            serializedObject.ApplyModifiedProperties();
            if (asToLoopPlay.floatValue >= 0f || bsToLoopPlay.floatValue >= 0f)
            {
                EditorGUILayout.HelpBox("AS／BS 播放到指定时间后开始接入 Loop；实际时间限制在入口混合结束与自然交接点之间。特殊互切和 TeamSwitch 沿用原规则。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("特殊互切总时长：0 按 AS/BS 自然时长播放，大于 0 使用定时方式。Default 按单段动画时长播放，仅使用入口和接入 Loop 混合；TeamSwitch 固定 Authored。短片段会自动缩短混合区间。", MessageType.Info);
            }
            if (float.IsNaN(asToLoopPlay.floatValue) || float.IsInfinity(asToLoopPlay.floatValue)
                || float.IsNaN(bsToLoopPlay.floatValue) || float.IsInfinity(bsToLoopPlay.floatValue))
            {
                EditorGUILayout.HelpBox("AS／BS 混合前播放时间必须是有限数值；负数表示自然时长。", MessageType.Error);
            }
            if (float.IsNaN(timedBlendAsToBs.floatValue) || float.IsInfinity(timedBlendAsToBs.floatValue))
            {
                EditorGUILayout.HelpBox("AS→BS 混合时长必须是有限的非负数。", MessageType.Error);
            }
            EditorGUILayout.HelpBox("以上参数供 Preview 和实际界面共用。非运行时修改可保存到场景或 Prefab。", MessageType.Info);
            if (!(timedBlendTotalDuration >= 0f) || float.IsInfinity(timedBlendTotalDuration)
                || !(timedBlendEnterDuration >= 0f) || float.IsInfinity(timedBlendEnterDuration)
                || !(timedBlendLoopDuration >= 0f) || float.IsInfinity(timedBlendLoopDuration)
                || (timedBlendTotalDuration > 0f
                    && timedBlendEnterDuration + timedBlendLoopDuration > timedBlendTotalDuration))
            {
                EditorGUILayout.HelpBox("时长必须为有限的非负数。定时模式的总时长不能短于入口与接入 Loop 混合时间之和；0 表示自然时长。", MessageType.Error);
            }
        }
        if (GUILayout.Button("打开角色展示测试窗口"))
        {
            CharacterPresentationTestWindow.Open((CharacterPresentationController)target);
        }
    }

}
