using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterPresentationController))]
public sealed class CharacterPresentationControllerEditor : Editor
{
    const string SessionKeyPrefix = "CharacterPresentationControllerEditor";

    CharacterPreviewActor testActor;
    AnimationClip testClip;
    FaceExpressionPreset testFacePreset;
    float testBlendDuration = 0.15f;
    string sessionKey;

    void OnEnable()
    {
        sessionKey = $"{SessionKeyPrefix}.{GlobalObjectId.GetGlobalObjectIdSlow(target)}";
        testActor = LoadObject<CharacterPreviewActor>("Actor");
        testClip = LoadObject<AnimationClip>("Clip");
        testFacePreset = LoadObject<FaceExpressionPreset>("FacePreset");
        testBlendDuration = SessionState.GetFloat(GetSessionKey("BlendDuration"), 0.15f);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("角色展示测试", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        testActor = (CharacterPreviewActor)EditorGUILayout.ObjectField(
            "测试角色",
            testActor,
            typeof(CharacterPreviewActor),
            true);
        testClip = (AnimationClip)EditorGUILayout.ObjectField(
            "身体动画",
            testClip,
            typeof(AnimationClip),
            false);
        testFacePreset = (FaceExpressionPreset)EditorGUILayout.ObjectField(
            "面部表情",
            testFacePreset,
            typeof(FaceExpressionPreset),
            false);
        testBlendDuration = Mathf.Max(
            0f,
            EditorGUILayout.FloatField("过渡时间", testBlendDuration));
        if (EditorGUI.EndChangeCheck())
        {
            SaveTestState();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 PlayMode 后可测试角色动画和表情。", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            CharacterPresentationController controller =
                (CharacterPresentationController)target;

            if (GUILayout.Button("自动绑定子角色"))
            {
                testActor = controller.GetComponentInChildren<CharacterPreviewActor>(true);
                SaveTestState();
                controller.Bind(testActor);
            }

            if (GUILayout.Button("绑定测试角色"))
            {
                controller.Bind(testActor);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("立即播放身体动画"))
            {
                controller.PlayImmediate(testClip);
            }

            if (GUILayout.Button("过渡播放身体动画"))
            {
                controller.CrossFadeTo(testClip, testBlendDuration);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("播放面部动画"))
            {
                controller.ApplyFacePreset(testFacePreset);
            }

            if (GUILayout.Button("同时播放身体和面部动画"))
            {
                controller.CrossFadeTo(testClip, testBlendDuration);
                controller.ApplyFacePreset(testFacePreset);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重置表情"))
            {
                controller.ResetFace();
            }

            if (GUILayout.Button("解除绑定"))
            {
                controller.Unbind();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    string GetSessionKey(string fieldName)
    {
        return $"{sessionKey}.{fieldName}";
    }

    T LoadObject<T>(string fieldName) where T : Object
    {
        string objectId = SessionState.GetString(GetSessionKey(fieldName), string.Empty);
        if (string.IsNullOrEmpty(objectId)
            || !GlobalObjectId.TryParse(objectId, out GlobalObjectId globalObjectId))
        {
            return null;
        }

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as T;
    }

    void SaveTestState()
    {
        SaveObject("Actor", testActor);
        SaveObject("Clip", testClip);
        SaveObject("FacePreset", testFacePreset);
        SessionState.SetFloat(GetSessionKey("BlendDuration"), testBlendDuration);
    }

    void SaveObject(string fieldName, Object value)
    {
        string objectId = value == null
            ? string.Empty
            : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        SessionState.SetString(GetSessionKey(fieldName), objectId);
    }
}
