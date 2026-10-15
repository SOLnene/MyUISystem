using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterPresentationController : MonoBehaviour
{
    enum TransitionStage
    {
        Idle,
        BlendingIntoClip,
        BlendingAsToBs,
        PlayingTransition,
        BlendingIntoLoop
    }

    readonly struct TransitionStep
    {
        internal readonly CharacterPresentationState State;
        internal readonly PresentationPhase Phase;
        internal readonly float StartAt;
        internal readonly float ClipStartTime;
        internal readonly float BlendDuration;

        internal TransitionStep(CharacterPresentationState state, PresentationPhase phase,
            float startAt, float blendDuration = 0f, float clipStartTime = 0f)
        {
            State = state;
            Phase = phase;
            StartAt = startAt;
            ClipStartTime = clipStartTime;
            BlendDuration = blendDuration;
        }
    }

    readonly struct TimedBlendSettings
    {
        internal readonly float TotalDuration;
        internal readonly float EnterDuration;
        internal readonly float LoopDuration;
        internal readonly float AsToBsDuration;

        internal bool IsValid => TotalDuration >= 0f && !float.IsInfinity(TotalDuration)
                                 && EnterDuration >= 0f && !float.IsInfinity(EnterDuration)
                                 && LoopDuration >= 0f && !float.IsInfinity(LoopDuration)
                                 && AsToBsDuration >= 0f && !float.IsInfinity(AsToBsDuration)
                                 && (TotalDuration == 0f || EnterDuration + LoopDuration <= TotalDuration);

        internal TimedBlendSettings(float totalDuration, float enterDuration, float loopDuration, float asToBsDuration)
        {
            TotalDuration = totalDuration;
            EnterDuration = enterDuration;
            LoopDuration = loopDuration;
            AsToBsDuration = asToBsDuration;
        }
    }

    [SerializeField]
    CharacterPreviewAnimator animationController;

    [SerializeField]
    FaceController faceController;

    [SerializeField, HideInInspector]
    float timedBlendTotalDuration = 1.3f;

    [SerializeField, HideInInspector]
    float timedBlendEnterDuration = 0.18f;

    [SerializeField, HideInInspector]
    float timedBlendLoopDuration = 0.12f;

    [SerializeField, HideInInspector]
    float timedBlendAsToBsDuration = 0.25f;

    CharacterPreviewActor actor;
    CharacterPresentationProfile profile;
    CharacterPresentationData currentPresentation;
    CharacterPresentationState currentState;
    CharacterPresentationState targetState;
    CharacterPresentationState playingState;
    PresentationPhase currentPhase;
    bool hasTargetPresentation;
    bool hasPlayingPresentation;
    CancellationTokenSource playbackCancellation;
    TransitionStage transitionStage;

    public CharacterPreviewActor Actor => actor;
    public CharacterPresentationProfile Profile => profile;
    public CharacterPresentationState CurrentState => currentState;
    public CharacterPresentationState TargetState => targetState;
    public CharacterPresentationState PlayingState => playingState;
    public PresentationPhase CurrentPhase => currentPhase;
    public bool HasCurrentPresentation => currentPresentation != null;
    public bool HasTargetPresentation => hasTargetPresentation;
    public bool HasPlayingPresentation => hasPlayingPresentation;

    public void Bind(
        CharacterPreviewActor targetActor,
        CharacterPresentationProfile targetProfile)
    {
        SetProfile(targetProfile);
        Bind(targetActor);
        if (targetActor != null && targetProfile != null)
        {
            SwitchAsync(
                CharacterPresentationState.Default,
                PresentationTransitionMode.Immediate).Forget();
        }
    }

    public void Bind(CharacterPreviewActor targetActor)
    {
        if (targetActor == null)
        {
            Unbind();
            return;
        }

        actor = targetActor;
        BindComponents(targetActor.Animator, targetActor.FaceRenderers);
    }

    public void Bind(Animator animator, SkinnedMeshRenderer[] faceRenderers)
    {
        actor = null;
        BindComponents(animator, faceRenderers);
    }

    void BindComponents(Animator animator, SkinnedMeshRenderer[] faceRenderers)
    {
        Stop();
        currentPresentation = null;
        currentPhase = PresentationPhase.None;
        animationController.Bind(animator);
        faceController.Bind(faceRenderers);
    }

    public void SetProfile(CharacterPresentationProfile targetProfile)
    {
        if (profile == targetProfile)
        {
            return;
        }

        Stop();
        profile = targetProfile;
        currentPresentation = null;
        currentPhase = PresentationPhase.None;
    }

    public void Unbind()
    {
        Stop();
        animationController.Unbind();
        faceController.Unbind();
        actor = null;
        profile = null;
        currentPresentation = null;
        currentPhase = PresentationPhase.None;
    }

    public UniTask SwitchAsync(
        CharacterPresentationState targetState,
        PresentationTransitionMode transitionMode,
        float? crossFadeDurationOverride = null,
        CancellationToken cancellationToken = default)
    {
        return SwitchInternalAsync(targetState, transitionMode, crossFadeDurationOverride, cancellationToken);
    }

    async UniTask SwitchInternalAsync(
        CharacterPresentationState targetState,
        PresentationTransitionMode transitionMode,
        float? crossFadeDurationOverride,
        CancellationToken cancellationToken)
    {
        if (profile == null)
        {
            Debug.LogWarning("Character presentation profile is not bound.");
            return;
        }

        // UI 负责阻塞切换点击；重复请求仅忽略，不保存目标或创建第二条播放路线。
        if (transitionStage != TransitionStage.Idle) return;
        if (currentPresentation != null && currentState == targetState
            && hasPlayingPresentation && currentPhase == PresentationPhase.Loop) return;

        TransitionStep[] plan = BuildTransitionPlan(targetState, transitionMode, crossFadeDurationOverride);
        if (plan.Length == 0) return;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, this.GetCancellationTokenOnDestroy());
        playbackCancellation = cancellation;
        SetTargetState(targetState);
        try
        {
            await ExecuteTransitionPlanAsync(plan, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            // 只有目标 Loop 真正建立后，后续切换才允许使用它的退出动画。
            currentPresentation = profile.Get(targetState);
            currentState = targetState;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (ReferenceEquals(playbackCancellation, cancellation))
            {
                currentPresentation = null;
                currentPhase = PresentationPhase.None;
                hasTargetPresentation = false;
                hasPlayingPresentation = false;
            }
        }
        finally
        {
            if (ReferenceEquals(playbackCancellation, cancellation))
            {
                playbackCancellation = null;
                transitionStage = TransitionStage.Idle;
            }
            // Stop 只发出取消；本次调用统一释放，避免旧请求清理新请求的状态。
            cancellation.Dispose();
        }
    }

    TransitionStep[] BuildTransitionPlan(
        CharacterPresentationState target,
        PresentationTransitionMode mode,
        float? crossFadeDurationOverride)
    {
        CharacterPresentationData destination = profile.Get(target);
        // 初始化和复位必须直接进入 Loop，不能被普通动作切换的路线规则覆盖。
        if (mode == PresentationTransitionMode.Immediate)
        {
            return new[] { new TransitionStep(target, PresentationPhase.Loop, 0f) };
        }
        bool hasSource = currentPresentation != null;
        bool teamRoute = target == CharacterPresentationState.TeamSwitch
                         || (hasSource && currentState == CharacterPresentationState.TeamSwitch);
        bool defaultRoute = hasSource && (currentState == CharacterPresentationState.Default
                                         || target == CharacterPresentationState.Default);
        if (teamRoute) mode = PresentationTransitionMode.Authored;
        else if (defaultRoute) mode = PresentationTransitionMode.AuthoredTimedBlend;
        else if (!hasSource && target == CharacterPresentationState.Default) mode = PresentationTransitionMode.Immediate;

        if (mode == PresentationTransitionMode.Immediate || mode == PresentationTransitionMode.CrossFade)
        {
            return new[] { new TransitionStep(target, PresentationPhase.Loop, 0f,
                mode == PresentationTransitionMode.CrossFade
                    ? ResolveCrossFadeDuration(destination, crossFadeDurationOverride) : 0f) };
        }

        float exitLength = hasSource ? GetPhaseDuration(currentPresentation, PresentationPhase.Exit) : 0f;
        float enterLength = GetPhaseDuration(destination, PresentationPhase.Enter);
        var steps = new List<TransitionStep>(3);
        if (mode == PresentationTransitionMode.Authored)
        {
            // 兼容原 Profile 的直接进入设置，仅生成步骤，不再保留独立执行分支。
            if (!teamRoute && hasSource && !defaultRoute && destination.UseDirectSpecialSwitch
                && destination.DirectSpecialEnterToLoopTime > 0f
                && (destination.EnterBodyClip != null || destination.EnterAuxiliaryClip != null)
                && (destination.LoopBodyClip != null || destination.LoopAuxiliaryClip != null))
            {
                float handoff = destination.DirectSpecialEnterToLoopTime;
                steps.Add(new TransitionStep(target, PresentationPhase.Enter, 0f,
                    Mathf.Min(ResolveCrossFadeDuration(destination, crossFadeDurationOverride), handoff * 0.5f)));
                steps.Add(new TransitionStep(target, PresentationPhase.Loop, handoff,
                    destination.DirectSpecialLoopBlendDuration));
            }
            else
            {
                if (exitLength > 0f)
                    steps.Add(new TransitionStep(currentState, PresentationPhase.Exit, 0f));
                if (enterLength > 0f)
                    steps.Add(new TransitionStep(target, PresentationPhase.Enter, exitLength));
                steps.Add(new TransitionStep(target, PresentationPhase.Loop, exitLength + enterLength));
            }
            return steps.ToArray();
        }

        bool useExit = hasSource && currentState != CharacterPresentationState.Default;
        bool useEnter = target != CharacterPresentationState.Default;
        bool bothPhases = useExit && useEnter;
        // Default 的辅助节点局部变换可能与目标首帧不同，不能硬切；单段 AS/BS 保留两端混合并按自身时长播放。
        var settings = new TimedBlendSettings(
            bothPhases ? timedBlendTotalDuration : 0f,
            timedBlendEnterDuration, timedBlendLoopDuration, bothPhases ? timedBlendAsToBsDuration : 0f);
        if (!settings.IsValid)
        {
            Debug.LogWarning("Invalid timed presentation blend durations.");
            return Array.Empty<TransitionStep>();
        }
        bool naturalTiming = settings.TotalDuration == 0f;
        if ((useExit && exitLength <= 0f) || (useEnter && enterLength <= 0f))
        {
            Debug.LogWarning($"Missing AS/BS for {currentState} -> {target}; blending directly to Loop.");
            return new[] { new TransitionStep(target, PresentationPhase.Loop, 0f,
                naturalTiming ? settings.LoopDuration : settings.TotalDuration) };
        }

        float entryBlend = settings.EnterDuration;
        float middleBlend = settings.AsToBsDuration;
        float loopBlend = settings.LoopDuration;
        if (naturalTiming)
        {
            // 入口结束即启动 BS，不等待 AS 尾段；短片段只压缩重叠区间，不加速动画。
            if (bothPhases) entryBlend = Mathf.Min(entryBlend, exitLength);
            float lastLength = useEnter ? enterLength : exitLength;
            float overlaps = (bothPhases ? middleBlend : entryBlend) + loopBlend;
            float scale = overlaps > 0f ? Mathf.Min(1f, lastLength / overlaps) : 1f;
            if (bothPhases) middleBlend *= scale;
            else entryBlend *= scale;
            loopBlend *= scale;
        }
        float bsTime = bothPhases ? entryBlend : 0f;
        float total = naturalTiming ? bsTime + (useEnter ? enterLength : exitLength) : settings.TotalDuration;
        float loopTime = Mathf.Max(0f, total - loopBlend);
        float bsOffset = naturalTiming ? 0f : Mathf.Max(0f, enterLength - (total - bsTime));
        if (useExit)
            steps.Add(new TransitionStep(currentState, PresentationPhase.Exit, 0f, entryBlend));
        if (useEnter)
            steps.Add(new TransitionStep(target, PresentationPhase.Enter, bsTime,
                bothPhases ? Mathf.Min(middleBlend, Mathf.Max(0f, loopTime - bsTime)) : entryBlend, bsOffset));
        steps.Add(new TransitionStep(target, PresentationPhase.Loop, loopTime, loopBlend));
        return steps.ToArray();
    }

    async UniTask ExecuteTransitionPlanAsync(TransitionStep[] plan, CancellationToken cancellationToken)
    {
        double startedAt = Time.timeAsDouble;
        for (int i = 0; i < plan.Length; i++)
        {
            TransitionStep step = plan[i];
            if (Time.timeAsDouble - startedAt < step.StartAt)
                await UniTask.WaitUntil(() => Time.timeAsDouble - startedAt >= step.StartAt,
                    cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            transitionStage = step.Phase == PresentationPhase.Loop ? TransitionStage.BlendingIntoLoop
                : i == 0 ? TransitionStage.BlendingIntoClip : TransitionStage.BlendingAsToBs;
            SetPlayingPhase(step.State, step.Phase);
            var clips = GetPhaseClips(profile.Get(step.State), step.Phase);
            // 以计划的绝对时间交接，帧误差不逐段累加；片段起点独立于混合权重。
            float blend = Mathf.Min(step.BlendDuration,
                Mathf.Max(0f, step.StartAt + step.BlendDuration - (float)(Time.timeAsDouble - startedAt)));
            PlayPhase(clips.body, clips.auxiliary, clips.faceA, clips.faceB, clips.faceSource,
                blend > 0f, blend, step.ClipStartTime, step.ClipStartTime,
                holdAtEnd: step.Phase != PresentationPhase.Loop);
            if (blend > 0f)
            {
                await animationController.WaitForLayeredBlendCompleteAsync(cancellationToken);
                // 只有表情资源时也必须等待其混合完成，不能依赖身体播放器的状态。
                if (Time.timeAsDouble - startedAt < step.StartAt + step.BlendDuration)
                    await UniTask.WaitUntil(() => Time.timeAsDouble - startedAt >= step.StartAt + step.BlendDuration,
                        cancellationToken: cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            transitionStage = step.Phase == PresentationPhase.Loop
                ? TransitionStage.Idle : TransitionStage.PlayingTransition;
        }
    }

    static (AnimationClip body, AnimationClip auxiliary, FaceExpressionPreset faceA,
        FaceExpressionPreset faceB, PresentationFaceSource faceSource)
        GetPhaseClips(CharacterPresentationData data, PresentationPhase phase)
    {
        return phase switch
        {
            PresentationPhase.Exit => (data.ExitBodyClip, data.ExitAuxiliaryClip, data.ExitFaceA, data.ExitFaceB, data.ExitFaceSource),
            PresentationPhase.Enter => (data.EnterBodyClip, data.EnterAuxiliaryClip, data.EnterFaceA, data.EnterFaceB, data.EnterFaceSource),
            PresentationPhase.Loop => (data.LoopBodyClip, data.LoopAuxiliaryClip, data.LoopFaceA, data.LoopFaceB, data.LoopFaceSource),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };
    }

    static float GetPhaseDuration(CharacterPresentationData data, PresentationPhase phase)
    {
        var clips = GetPhaseClips(data, phase);
        return clips.body != null || clips.auxiliary != null
            ? GetPhaseClipLength(clips.body, clips.auxiliary)
            : GetFacePhaseDuration(clips.faceA, clips.faceB);
    }

    void PlayPhase(
        AnimationClip bodyClip,
        AnimationClip auxiliaryClip,
        FaceExpressionPreset faceA,
        FaceExpressionPreset faceB,
        PresentationFaceSource faceSource,
        bool crossFade,
        float crossFadeDuration,
        float bodyStartTime = 0f,
        float auxiliaryStartTime = 0f,
        bool holdAtEnd = false)
    {
        if (bodyClip != null || auxiliaryClip != null)
        {
            animationController.PlayLayered(
                bodyClip,
                auxiliaryClip,
                crossFade,
                crossFadeDuration,
                bodyStartTime,
                auxiliaryStartTime,
                holdAtEnd);
        }

        if (faceSource == PresentationFaceSource.Embedded)
        {
            faceController.SetPresentationFacePlayback(false);
            return;
        }
        if (faceSource == PresentationFaceSource.None)
        {
            faceController.SetPresentationFacePlayback(true);
            faceController.PlayPresentationFace(null, null, 0f, 0f);
            return;
        }
        bool continuePreset = faceController.IsPresentationFacePlaybackEnabled;
        faceController.SetPresentationFacePlayback(true);
        float faceStartTime = bodyClip != null ? bodyStartTime : auxiliaryStartTime;
        if (faceA == null && faceB == null)
        {
            // 整份表情缺失时回退角色默认，而不是保留上一状态的眼睛或嘴部值。
            faceA = profile?.DefaultPresentation.LoopFaceA;
            faceB = profile?.DefaultPresentation.LoopFaceB;
            faceStartTime = 0f;
        }
        faceController.PlayPresentationFace(
            faceA, faceB, faceStartTime, crossFade && continuePreset ? crossFadeDuration : 0f);
    }

    static float GetFacePhaseDuration(
        FaceExpressionPreset faceA,
        FaceExpressionPreset faceB)
    {
        return Mathf.Max(
            faceA == null ? 0f : faceA.duration,
            faceB == null ? 0f : faceB.duration);
    }

    static float GetPhaseClipLength(AnimationClip bodyClip, AnimationClip auxiliaryClip)
    {
        return bodyClip != null ? bodyClip.length : auxiliaryClip != null ? auxiliaryClip.length : 0f;
    }

    static float ResolveCrossFadeDuration(
        CharacterPresentationData presentation,
        float? crossFadeDurationOverride)
    {
        return Mathf.Max(
            0f,
            crossFadeDurationOverride ?? presentation.CrossFadeDuration);
    }

    void SetTargetState(CharacterPresentationState state)
    {
        targetState = state;
        hasTargetPresentation = true;
    }

    void SetPlayingPhase(
        CharacterPresentationState state,
        PresentationPhase phase)
    {
        playingState = state;
        currentPhase = phase;
        hasPlayingPresentation = true;
    }

    public void Stop()
    {
        currentPresentation = null;
        currentPhase = PresentationPhase.None;
        hasTargetPresentation = false;
        hasPlayingPresentation = false;
        transitionStage = TransitionStage.Idle;
        CancellationTokenSource cancellation = playbackCancellation;
        playbackCancellation = null;
        if (cancellation == null)
        {
            return;
        }

        cancellation.Cancel();
    }

    public void PlayImmediate(AnimationClip clip)
    {
        animationController.PlayImmediate(clip);
    }

    public void CrossFadeTo(
        AnimationClip clip,
        float blendDuration,
        Action onBlendCompleted = null)
    {
        animationController.CrossFadeTo(clip, blendDuration, onBlendCompleted);
    }

    public void ApplyFacePreset(FaceExpressionPreset preset)
    {
        faceController.SetPresentationFacePlayback(true);
        faceController.ApplyFacePreset(preset);
    }

    void OnDisable()
    {
        Stop();
    }

    void OnDestroy()
    {
        Stop();
    }
}
