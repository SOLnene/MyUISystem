using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterPresentationController))]
public sealed class CharacterPresentationControllerEditor : Editor
{
    const string SessionKeyPrefix = "CharacterPresentationControllerEditor";

    CharacterPreviewActor testActor;
    CharacterPresentationProfile testProfile;
    CharacterPresentationState testState;
    PresentationTransitionMode testTransitionMode =
        PresentationTransitionMode.Authored;
    bool overrideCrossFadeDuration;
    AnimationClip testClip;
    FaceExpressionPreset testFacePresetA;
    FaceExpressionPreset testFacePresetB;
    float testBlendDuration = 0.15f;
    string sessionKey;

    void OnEnable()
    {
        sessionKey = $"{SessionKeyPrefix}.{GlobalObjectId.GetGlobalObjectIdSlow(target)}";
        testActor = LoadObject<CharacterPreviewActor>("Actor");
        testProfile = LoadObject<CharacterPresentationProfile>("Profile");
        testState = (CharacterPresentationState)SessionState.GetInt(
            GetSessionKey("State"),
            (int)CharacterPresentationState.Default);
        testTransitionMode = (PresentationTransitionMode)SessionState.GetInt(
            GetSessionKey("TransitionMode"),
            (int)PresentationTransitionMode.Authored);
        overrideCrossFadeDuration = SessionState.GetBool(
            GetSessionKey("OverrideCrossFadeDuration"),
            false);
        testClip = LoadObject<AnimationClip>("Clip");
        testFacePresetA = LoadObject<FaceExpressionPreset>("FacePresetA")
                          ?? LoadObject<FaceExpressionPreset>("FacePreset");
        testFacePresetB = LoadObject<FaceExpressionPreset>("FacePresetB");
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
        testProfile = (CharacterPresentationProfile)EditorGUILayout.ObjectField(
            "展示配置",
            testProfile,
            typeof(CharacterPresentationProfile),
            false);
        testState = (CharacterPresentationState)EditorGUILayout.EnumPopup(
            "目标状态",
            testState);
        testTransitionMode = (PresentationTransitionMode)EditorGUILayout.EnumPopup(
            "切换方式",
            testTransitionMode);
        using (new EditorGUI.DisabledScope(
                   testTransitionMode == PresentationTransitionMode.Immediate))
        {
            overrideCrossFadeDuration = EditorGUILayout.Toggle(
                "覆盖配置过渡时间",
                overrideCrossFadeDuration);
            using (new EditorGUI.DisabledScope(!overrideCrossFadeDuration))
            {
                testBlendDuration = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField("过渡时间", testBlendDuration));
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("单项测试", EditorStyles.boldLabel);
        testClip = (AnimationClip)EditorGUILayout.ObjectField(
            "身体动画",
            testClip,
            typeof(AnimationClip),
            false);
        testFacePresetA = (FaceExpressionPreset)EditorGUILayout.ObjectField(
            "面部动画 A",
            testFacePresetA,
            typeof(FaceExpressionPreset),
            false);
        testFacePresetB = (FaceExpressionPreset)EditorGUILayout.ObjectField(
            "面部动画 B",
            testFacePresetB,
            typeof(FaceExpressionPreset),
            false);
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
                BindTestActor(controller);
            }

            if (GUILayout.Button("绑定测试角色与配置"))
            {
                BindTestActor(controller);
            }

            if (controller.HasCurrentPresentation)
            {
                EditorGUILayout.LabelField("当前状态", controller.CurrentState.ToString());
                EditorGUILayout.LabelField("当前阶段", controller.CurrentPhase.ToString());
            }

            if (GUILayout.Button("切换到目标状态"))
            {
                SwitchPresentation(controller);
            }

            if (GUILayout.Button("停止展示流程"))
            {
                controller.Stop();
            }

            EditorGUILayout.Space(6f);

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
                PlayFacePresets(controller);
            }

            if (GUILayout.Button("同时播放身体和面部动画"))
            {
                controller.CrossFadeTo(testClip, testBlendDuration);
                PlayFacePresets(controller);
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
        SaveObject("Profile", testProfile);
        SaveObject("Clip", testClip);
        SaveObject("FacePresetA", testFacePresetA);
        SaveObject("FacePresetB", testFacePresetB);
        SessionState.SetInt(GetSessionKey("State"), (int)testState);
        SessionState.SetInt(
            GetSessionKey("TransitionMode"),
            (int)testTransitionMode);
        SessionState.SetBool(
            GetSessionKey("OverrideCrossFadeDuration"),
            overrideCrossFadeDuration);
        SessionState.SetFloat(GetSessionKey("BlendDuration"), testBlendDuration);
    }

    void BindTestActor(CharacterPresentationController controller)
    {
        if (testProfile != null)
        {
            controller.Bind(testActor, testProfile);
            return;
        }

        controller.Bind(testActor);
    }

    void SwitchPresentation(CharacterPresentationController controller)
    {
        float? durationOverride = overrideCrossFadeDuration
            ? testBlendDuration
            : null;
        controller.SwitchAsync(
            testState,
            testTransitionMode,
            durationOverride).Forget();
    }

    void PlayFacePresets(CharacterPresentationController controller)
    {
        if (testFacePresetA != null && testFacePresetB != null)
        {
            controller.ApplyFacePresets(testFacePresetA, testFacePresetB);
            return;
        }

        FaceExpressionPreset preset = testFacePresetA != null
            ? testFacePresetA
            : testFacePresetB;
        if (preset != null)
        {
            controller.ApplyFacePreset(preset);
            return;
        }

        Debug.LogWarning("未选择面部动画");
    }

    void SaveObject(string fieldName, Object value)
    {
        string objectId = value == null
            ? string.Empty
            : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        SessionState.SetString(GetSessionKey(fieldName), objectId);
    }
}
