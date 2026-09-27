using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterPresentationController))]
public sealed class CharacterPresentationControllerEditor : Editor
{
    SerializedProperty timedBlendTotal;
    SerializedProperty timedBlendEnter;
    SerializedProperty timedBlendLoop;
    SerializedProperty timedBlendAsToBs;
    float timedBlendTotalDuration => timedBlendTotal.floatValue;
    float timedBlendEnterDuration => timedBlendEnter.floatValue;
    float timedBlendLoopDuration => timedBlendLoop.floatValue;
    void OnEnable()
    {
        timedBlendTotal = serializedObject.FindProperty("timedBlendTotalDuration");
        timedBlendEnter = serializedObject.FindProperty("timedBlendEnterDuration");
        timedBlendLoop = serializedObject.FindProperty("timedBlendLoopDuration");
        timedBlendAsToBs = serializedObject.FindProperty("timedBlendAsToBsDuration");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("动画切换设置", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            serializedObject.Update();
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
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("特殊互切总时长：0 按 AS/BS 自然时长播放，大于 0 使用定时方式。Default 按单段动画时长播放，仅使用入口和接入 Loop 混合；TeamSwitch 固定 Authored。短片段会自动缩短混合区间。", MessageType.Info);
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
