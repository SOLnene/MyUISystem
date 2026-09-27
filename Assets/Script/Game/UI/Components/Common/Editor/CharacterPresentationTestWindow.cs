using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class CharacterPresentationTestWindow : EditorWindow
{
    const string SessionKeyPrefix = "CharacterPresentationControllerEditor";

    [SerializeField] CharacterPresentationController targetController;
    [SerializeField] string targetGlobalId;
    SerializedObject serializedObject;
    CharacterPreviewAnimator testAnimator;
    FaceController testFaceController;
    Vector2 scrollPosition;
    Object target => targetController;

    [MenuItem("Tools/MyUISystem/角色展示/测试/角色展示测试", false, 340)]
    static void ShowWindow()
    {
        Open(Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<CharacterPresentationController>()
            : null);
    }

    public static void Open(CharacterPresentationController controller)
    {
        var window = GetWindow<CharacterPresentationTestWindow>("角色展示测试");
        if (controller != null) window.targetController = controller;
        window.InitializeTarget();
        window.Show();
    }

    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += RestoreMissingTarget;
        InitializeTarget();
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= RestoreMissingTarget;
        EditorApplication.delayCall -= RestoreTarget;
        serializedObject?.Dispose();
        serializedObject = null;
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            // 场景对象在 PlayMode 切换时会重建，下一次编辑器更新重新取得绑定。
            EditorApplication.delayCall -= RestoreTarget;
            EditorApplication.delayCall += RestoreTarget;
        }
    }

    void RestoreTarget()
    {
        if (this == null) return;
        InitializeTarget();
        Repaint();
    }

    void RestoreMissingTarget()
    {
        if (targetController == null && !string.IsNullOrEmpty(targetGlobalId))
            InitializeTarget();
    }

    void OnInspectorUpdate()
    {
        if (Application.isPlaying) Repaint();
    }

    void OnGUI()
    {
        RestoreMissingTarget();
        EditorGUI.BeginChangeCheck();
        var selected = (CharacterPresentationController)EditorGUILayout.ObjectField(
            "目标控制器", targetController, typeof(CharacterPresentationController), true);
        bool changed = EditorGUI.EndChangeCheck();
        if (GUILayout.Button("使用当前选择"))
        {
            selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<CharacterPresentationController>()
                : null;
            changed = true;
        }
        if (changed)
        {
            if (targetController != null && serializedObject != null) SaveTestState();
            targetController = selected;
            targetGlobalId = string.Empty;
            InitializeTarget();
        }

        if (targetController == null || EditorUtility.IsPersistent(targetController))
        {
            EditorGUILayout.HelpBox("请选择场景中的角色展示控制器。", MessageType.Info);
            return;
        }

        if (serializedObject == null || serializedObject.targetObject != targetController)
            InitializeTarget();
        serializedObject.Update();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawTests();
        EditorGUILayout.EndScrollView();
    }

    ModelPreviewController testPreviewController;
    ModelPreviewTestBootstrap testBootstrap;
    ModelPreviewDefinition testDefinition;
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
    SerializedProperty timedBlendTotal;
    SerializedProperty timedBlendEnter;
    SerializedProperty timedBlendLoop;
    SerializedProperty timedBlendAsToBs;
    float timedBlendTotalDuration => timedBlendTotal.floatValue;
    float timedBlendEnterDuration => timedBlendEnter.floatValue;
    float timedBlendLoopDuration => timedBlendLoop.floatValue;
    string sessionKey;

    void InitializeTarget()
    {
        serializedObject?.Dispose();
        serializedObject = null;
        // 域重载可能先清空场景引用，用持久标识恢复同一个控制器，不选取其他角色。
        if (targetController == null
            && !string.IsNullOrEmpty(targetGlobalId)
            && GlobalObjectId.TryParse(targetGlobalId, out GlobalObjectId globalId))
        {
            targetController = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId)
                as CharacterPresentationController;
        }
        if (targetController == null) return;
        targetGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(targetController).ToString();
        serializedObject = new SerializedObject(targetController);
        testAnimator = (CharacterPreviewAnimator)serializedObject.FindProperty("animationController").objectReferenceValue;
        testFaceController = (FaceController)serializedObject.FindProperty("faceController").objectReferenceValue;
        CharacterPresentationController controller =
            (CharacterPresentationController)target;
        sessionKey = $"{SessionKeyPrefix}.{GlobalObjectId.GetGlobalObjectIdSlow(target)}";
        testPreviewController = LoadObject<ModelPreviewController>("PreviewController");
        if (testPreviewController == null)
        {
            testPreviewController =
                controller.GetComponent<ModelPreviewController>();
        }
        testBootstrap =
            controller.GetComponent<ModelPreviewTestBootstrap>();
        testDefinition = LoadObject<ModelPreviewDefinition>("Definition");
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
        timedBlendTotal = serializedObject.FindProperty("timedBlendTotalDuration");
        timedBlendEnter = serializedObject.FindProperty("timedBlendEnterDuration");
        timedBlendLoop = serializedObject.FindProperty("timedBlendLoopDuration");
        timedBlendAsToBs = serializedObject.FindProperty("timedBlendAsToBsDuration");
        if (Application.isPlaying && controller.Actor != null)
        {
            SynchronizeCurrentBinding(controller);
        }
    }

    void DrawTests()
    {

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("角色展示测试", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        testPreviewController = (ModelPreviewController)EditorGUILayout.ObjectField(
            "模型预览控制器",
            testPreviewController,
            typeof(ModelPreviewController),
            true);
        ModelPreviewDefinition previousDefinition = testDefinition;
        testDefinition = (ModelPreviewDefinition)EditorGUILayout.ObjectField(
            "角色配置",
            testDefinition,
            typeof(ModelPreviewDefinition),
            false);
        if (testDefinition != previousDefinition)
        {
            testProfile = testDefinition != null
                ? testDefinition.PresentationProfile
                : null;
        }
        if (testDefinition != null)
        {
            EditorGUILayout.LabelField(
                "自动 Profile",
                testDefinition.PresentationProfile != null
                    ? testDefinition.PresentationProfile.name
                    : "未配置");
        }

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
        bool teamRoute = testState == CharacterPresentationState.TeamSwitch
                         || (targetController.HasCurrentPresentation
                             && targetController.CurrentState == CharacterPresentationState.TeamSwitch);
        bool defaultRoute = targetController.HasCurrentPresentation
                            && (testState == CharacterPresentationState.Default
                                || targetController.CurrentState == CharacterPresentationState.Default);
        bool authoredOnlyRoute = teamRoute || defaultRoute;
        if (teamRoute)
        {
            EditorGUILayout.LabelField("切换方式", "Authored（TeamSwitch 固定）");
        }
        else if (defaultRoute)
        {
            EditorGUILayout.LabelField("切换方式", "AuthoredTimedBlend（Default 自然时长＋两端混合）");
        }
        else
        {
            testTransitionMode = (PresentationTransitionMode)EditorGUILayout.EnumPopup(
                "切换方式",
                testTransitionMode);
        }
        if (!teamRoute && (defaultRoute || testTransitionMode == PresentationTransitionMode.AuthoredTimedBlend))
        {
            EditorGUILayout.LabelField("切换参数", $"{timedBlendTotalDuration:0.###} / {timedBlendEnterDuration:0.###} / {timedBlendLoopDuration:0.###} 秒");
            EditorGUILayout.LabelField("时间规则", defaultRoute || timedBlendTotalDuration == 0f ? "自然时长" : "定时切换");
            if (!defaultRoute)
                EditorGUILayout.LabelField("AS→BS 混合时长", $"{timedBlendAsToBs.floatValue:0.###} 秒");
            EditorGUILayout.HelpBox("时长读取目标控制器的正式配置，请在该组件的 Inspector 中调整。", MessageType.Info);
        }
        else if (!authoredOnlyRoute)
        {
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

        using (new EditorGUI.DisabledScope(!Application.isPlaying || testAnimator == null || testFaceController == null))
        {
            CharacterPresentationController controller =
                (CharacterPresentationController)target;

            using (new EditorGUI.DisabledScope(
                       testPreviewController == null
                       || (testBootstrap != null && !testBootstrap.IsReady)
                       || testPreviewController.IsLoading
                       || testDefinition == null
                       || testDefinition.PreviewType != ModelPreviewType.Character))
            {
                if (GUILayout.Button("加载/更换模型"))
                {
                    LoadSelectedModelAsync().Forget(Debug.LogException);
                }
            }

            if (testPreviewController != null)
            {
                if (testBootstrap != null)
                {
                    EditorGUILayout.LabelField(
                        "资源系统",
                        testBootstrap.IsReady ? "Ready" : "Initializing");
                }

                EditorGUILayout.LabelField(
                    "当前模型",
                    testPreviewController.ActivePreviewDefinition != null
                        ? testPreviewController.ActivePreviewDefinition.name
                        : "未加载");
                EditorGUILayout.LabelField(
                    "加载状态",
                    GetLoadState(testPreviewController));
            }

            if (GUILayout.Button("自动绑定子角色"))
            {
                SynchronizeCurrentBinding(controller);
            }

            if (GUILayout.Button("绑定测试角色与配置"))
            {
                BindTestActor(controller);
            }

            if (controller.HasTargetPresentation)
            {
                EditorGUILayout.LabelField("目标状态", controller.TargetState.ToString());
            }

            if (controller.HasPlayingPresentation)
            {
                EditorGUILayout.LabelField(
                    "正在播放",
                    $"{controller.PlayingState}.{GetPhaseName(controller.CurrentPhase)}");
            }

            bool invalidTimedBlend = !teamRoute && (defaultRoute || testTransitionMode == PresentationTransitionMode.AuthoredTimedBlend)
                                     && (!(timedBlendTotalDuration >= 0f)
                                         || float.IsInfinity(timedBlendTotalDuration)
                                         || !(timedBlendEnterDuration >= 0f) || float.IsInfinity(timedBlendEnterDuration)
                                         || !(timedBlendLoopDuration >= 0f) || float.IsInfinity(timedBlendLoopDuration)
                                         || !(timedBlendAsToBs.floatValue >= 0f)
                                         || float.IsInfinity(timedBlendAsToBs.floatValue)
                                         || (timedBlendTotalDuration > 0f
                                             && timedBlendEnterDuration + timedBlendLoopDuration > timedBlendTotalDuration));
            using (new EditorGUI.DisabledScope(invalidTimedBlend))
            {
                if (GUILayout.Button("切换到目标状态"))
                {
                    SwitchPresentation(controller);
                }
            }

            if (GUILayout.Button("停止展示流程"))
            {
                controller.Stop();
            }

            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("立即播放身体动画"))
            {
                controller.Stop();
                testAnimator.PlayImmediate(testClip);
            }

            if (GUILayout.Button("过渡播放身体动画"))
            {
                controller.Stop();
                testAnimator.CrossFadeTo(testClip, testBlendDuration);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("播放面部动画"))
            {
                PlayFacePresets(testFaceController);
            }

            if (GUILayout.Button("同时播放身体和面部动画"))
            {
                controller.Stop();
                testAnimator.CrossFadeTo(testClip, testBlendDuration);
                PlayFacePresets(testFaceController);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重置表情"))
            {
                testFaceController.ResetAll();
            }

            if (GUILayout.Button("解除绑定"))
            {
                controller.Unbind();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    static string GetPhaseName(PresentationPhase phase)
    {
        return phase switch
        {
            PresentationPhase.Exit => "AS",
            PresentationPhase.Enter => "BS",
            PresentationPhase.Loop => "Loop",
            _ => "None"
        };
    }

    static string GetLoadState(ModelPreviewController controller)
    {
        if (controller.IsLoading)
        {
            return "Loading";
        }

        if (controller.HasLoadFailed)
        {
            return "Failed";
        }

        return controller.ActivePreviewDefinition != null ? "Loaded" : "Idle";
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
        SaveObject("PreviewController", testPreviewController);
        SaveObject("Definition", testDefinition);
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

    async UniTask LoadSelectedModelAsync()
    {
        CharacterPresentationController requestedController = targetController;
        ModelPreviewDefinition loadedDefinition =
            await testPreviewController.ShowAsync(testDefinition);
        if (loadedDefinition == null)
        {
            Debug.LogWarning("测试模型加载失败");
            return;
        }

        if (targetController != requestedController) return;
        CharacterPresentationController controller =
            (CharacterPresentationController)target;
        testDefinition = loadedDefinition;
        SynchronizeCurrentBinding(controller);
    }

    void BindTestActor(CharacterPresentationController controller)
    {
        CharacterPresentationProfile profile = testProfile != null
            ? testProfile
            : testDefinition?.PresentationProfile;
        controller.Bind(testActor, profile);
    }

    void SynchronizeCurrentBinding(CharacterPresentationController controller)
    {
        // 当前预览的运行时绑定是唯一事实来源，避免命中缓存层级中的旧角色。
        testActor = controller.Actor;
        testProfile = controller.Profile;
        ModelPreviewDefinition activeDefinition =
            testPreviewController?.ActivePreviewDefinition;
        if (activeDefinition != null
            && activeDefinition.PreviewType == ModelPreviewType.Character)
        {
            testDefinition = activeDefinition;
            if (testProfile == null)
            {
                testProfile = activeDefinition.PresentationProfile;
            }
        }

        SaveTestState();
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

    void PlayFacePresets(FaceController controller)
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
