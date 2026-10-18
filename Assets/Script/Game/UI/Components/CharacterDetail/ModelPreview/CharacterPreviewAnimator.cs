using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 负责模型预览ui界面切换animaotr中的clip
/// </summary>
public class CharacterPreviewAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [SerializeField]
    private AnimatorOverrideController overrideController;

    private AnimatorOverrideController runtimeOverrideController;
    private RuntimeAnimatorController originalAnimatorController;
    private bool originalApplyRootMotion;
    private const string STATE_NAME = "Idle";

    private Coroutine idleRoutine;
    private Coroutine completeRoutine;
    bool isPlayingStateA = true; // 标记当前在播放 StateA 还是 StateB

    #region Playable
      private PlayableGraph graph;
      private AnimationLayerMixerPlayable layerMixer;
      private AnimationMixerPlayable bodyMixer;
      private AnimationMixerPlayable auxiliaryMixer;
    
      private AnimationClipPlayable currentBodyPlayable;
      private AnimationClipPlayable nextBodyPlayable;
      private AnimationClipPlayable currentAuxiliaryPlayable;
      private AnimationClipPlayable nextAuxiliaryPlayable;
      private float blendTime;
      private float blendTimer;
      private bool isBlending;
      private Action playableBlendCompleted;
      readonly HashSet<AnimationClipPlayable> transitionPlayables = new();

  #endregion
    void Awake()
    {
        Animator initialAnimator = animator;
        animator = null;
        if (initialAnimator != null)
        {
            Bind(initialAnimator);
        }
    }

    public void Bind(Animator targetAnimator)
    {
        Unbind();
        animator = targetAnimator;
        if (animator == null)
        {
            return;
        }

        originalAnimatorController = animator.runtimeAnimatorController;
        originalApplyRootMotion = animator.applyRootMotion;
        if (overrideController != null)
        {
            runtimeOverrideController = Instantiate(overrideController);
        }
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false; // 防止飞走
        isPlayingStateA = true;
        InitializePlayableGraph();
    }

    public void Unbind()
    {
        StopActiveRoutines();
        DestroyPlayableGraph();
        if (animator != null)
        {
            animator.runtimeAnimatorController = originalAnimatorController;
            animator.applyRootMotion = originalApplyRootMotion;
            // A destroyed graph leaves its last pose behind; reset before another preview borrows this actor.
            animator.Rebind();
            if (animator.isActiveAndEnabled)
            {
                animator.Update(0f);
            }
        }

        ReleaseRuntimeController();
        animator = null;
        originalAnimatorController = null;
        isPlayingStateA = true;
    }

    void StopActiveRoutines()
    {
        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }

        if (completeRoutine != null)
        {
            StopCoroutine(completeRoutine);
            completeRoutine = null;
        }
    }

    void ReleaseRuntimeController()
    {
        if (runtimeOverrideController == null)
        {
            return;
        }

        Destroy(runtimeOverrideController);
        runtimeOverrideController = null;
    }

    void OnDestroy()
    {
        Unbind();
    }

    void Update()
    {
        foreach (AnimationClipPlayable playable in transitionPlayables)
        {
            double endTime = Math.Max(0d, playable.GetAnimationClip().length - 0.000001d);
            if (playable.GetTime() + Time.deltaTime >= endTime)
            {
                // 在图本帧求值前停在末帧，避免带 Loop 标记的过渡资源回绕到开头。
                playable.SetTime(endTime);
                playable.SetSpeed(0d);
            }
        }

        if (!isBlending || !layerMixer.IsValid())
        {
            return;
        }

        blendTimer += Time.deltaTime;
        float progress = blendTime <= 0f
            ? 1f
            : Mathf.Clamp01(blendTimer / blendTime);
        SetBlendWeights(
            bodyMixer,
            currentBodyPlayable,
            nextBodyPlayable,
            progress);
        SetBlendWeights(
            auxiliaryMixer,
            currentAuxiliaryPlayable,
            nextAuxiliaryPlayable,
            progress);

        if (progress >= 1f)
        {
            CompletePlayableBlend(true);
        }
    }

    void InitializePlayableGraph()
    {
        DestroyPlayableGraph();
        graph = PlayableGraph.Create($"{name}.CharacterPreview");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);
        bodyMixer = AnimationMixerPlayable.Create(graph, 2);
        auxiliaryMixer = AnimationMixerPlayable.Create(graph, 2);
        graph.Connect(bodyMixer, 0, layerMixer, 0);
        graph.Connect(auxiliaryMixer, 0, layerMixer, 1);
        layerMixer.SetInputWeight(0, 1f);
        layerMixer.SetInputWeight(1, 0f);
        // 提取出的辅助动画记录绝对局部变换，不能按增量动画叠加。
        layerMixer.SetLayerAdditive(1, false);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            graph,
            "CharacterPreview",
            animator);
        output.SetSourcePlayable(layerMixer);
        ResetMixerWeights(bodyMixer);
        ResetMixerWeights(auxiliaryMixer);
        graph.Play();
    }

    void DestroyPlayableGraph()
    {
        transitionPlayables.Clear();
        isBlending = false;
        playableBlendCompleted = null;
        currentBodyPlayable = default;
        nextBodyPlayable = default;
        currentAuxiliaryPlayable = default;
        nextAuxiliaryPlayable = default;
        if (graph.IsValid())
        {
            graph.Destroy();
        }

        graph = default;
        layerMixer = default;
        bodyMixer = default;
        auxiliaryMixer = default;
    }

    AnimationClipPlayable CreatePlayable(AnimationClip clip, float startTime = 0f)
    {
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetTime(Mathf.Clamp(startTime, 0f, clip.length));
        playable.SetSpeed(1d);
        return playable;
    }

    AnimationClipPlayable PlayImmediateWithPlayable(AnimationClip clip)
    {
        PlayImmediateLayeredWithPlayable(clip, null);
        return currentBodyPlayable;
    }

    AnimationClipPlayable CrossFadeWithPlayable(
        AnimationClip clip,
        float duration,
        Action onBlendCompleted)
    {
        CrossFadeLayeredWithPlayable(
            clip,
            null,
            duration,
            onBlendCompleted);
        return isBlending ? nextBodyPlayable : currentBodyPlayable;
    }

    void PlayImmediateLayeredWithPlayable(
        AnimationClip bodyClip,
        AnimationClip auxiliaryClip,
        float bodyStartTime = 0f,
        float auxiliaryStartTime = 0f)
    {
        CancelPlayableBlend();
        ReplaceChannelImmediate(
            bodyMixer,
            ref currentBodyPlayable,
            ref nextBodyPlayable,
            bodyClip,
            bodyStartTime);
        ReplaceChannelImmediate(
            auxiliaryMixer,
            ref currentAuxiliaryPlayable,
            ref nextAuxiliaryPlayable,
            auxiliaryClip,
            auxiliaryStartTime);
        UpdateAuxiliaryLayerWeight();
        graph.Evaluate(0f);
    }

    void CrossFadeLayeredWithPlayable(
        AnimationClip bodyClip,
        AnimationClip auxiliaryClip,
        float duration,
        Action onBlendCompleted,
        float bodyStartTime = 0f,
        float auxiliaryStartTime = 0f)
    {
        bool hasCurrentLayer = currentBodyPlayable.IsValid()
                               || currentAuxiliaryPlayable.IsValid();
        if (!hasCurrentLayer || duration <= 0f)
        {
            PlayImmediateLayeredWithPlayable(
                bodyClip,
                auxiliaryClip,
                bodyStartTime,
                auxiliaryStartTime);
            onBlendCompleted?.Invoke();
            return;
        }

        if (isBlending)
        {
            CompletePlayableBlend(false);
        }

        PrepareChannelBlend(
            bodyMixer,
            currentBodyPlayable,
            ref nextBodyPlayable,
            bodyClip,
            bodyStartTime);
        PrepareChannelBlend(
            auxiliaryMixer,
            currentAuxiliaryPlayable,
            ref nextAuxiliaryPlayable,
            auxiliaryClip,
            auxiliaryStartTime);
        UpdateAuxiliaryLayerWeight();
        blendTime = duration;
        blendTimer = 0f;
        playableBlendCompleted = onBlendCompleted;
        isBlending = true;
    }

    void ReplaceChannelImmediate(
        AnimationMixerPlayable targetMixer,
        ref AnimationClipPlayable current,
        ref AnimationClipPlayable next,
        AnimationClip clip,
        float startTime)
    {
        DestroyPlayable(targetMixer, ref current, 0);
        DestroyPlayable(targetMixer, ref next, 1);
        if (clip != null)
        {
            current = CreatePlayable(clip, startTime);
            graph.Connect(current, 0, targetMixer, 0);
        }

        targetMixer.SetInputWeight(0, current.IsValid() ? 1f : 0f);
        targetMixer.SetInputWeight(1, 0f);
    }

    void PrepareChannelBlend(
        AnimationMixerPlayable targetMixer,
        AnimationClipPlayable current,
        ref AnimationClipPlayable next,
        AnimationClip clip,
        float startTime)
    {
        DestroyPlayable(targetMixer, ref next, 1);
        if (clip != null)
        {
            next = CreatePlayable(clip, startTime);
            graph.Connect(next, 0, targetMixer, 1);
        }

        targetMixer.SetInputWeight(0, current.IsValid() ? 1f : 0f);
        targetMixer.SetInputWeight(1, 0f);
    }

    void CancelPlayableBlend()
    {
        if (!isBlending)
        {
            return;
        }

        CompletePlayableBlend(false);
    }

    void CompletePlayableBlend(bool invokeCallback)
    {
        if (!isBlending)
        {
            return;
        }

        Action completion = playableBlendCompleted;
        playableBlendCompleted = null;
        isBlending = false;
        CompleteChannelBlend(
            bodyMixer,
            ref currentBodyPlayable,
            ref nextBodyPlayable);
        CompleteChannelBlend(
            auxiliaryMixer,
            ref currentAuxiliaryPlayable,
            ref nextAuxiliaryPlayable);
        UpdateAuxiliaryLayerWeight();

        if (invokeCallback)
        {
            completion?.Invoke();
        }
    }

    void CompleteChannelBlend(
        AnimationMixerPlayable targetMixer,
        ref AnimationClipPlayable current,
        ref AnimationClipPlayable next)
    {
        DestroyPlayable(targetMixer, ref current, 0);
        if (next.IsValid())
        {
            targetMixer.DisconnectInput(1);
            current = next;
            next = default;
            graph.Connect(current, 0, targetMixer, 0);
        }

        targetMixer.SetInputWeight(0, current.IsValid() ? 1f : 0f);
        targetMixer.SetInputWeight(1, 0f);
    }

    static void SetBlendWeights(
        AnimationMixerPlayable targetMixer,
        AnimationClipPlayable current,
        AnimationClipPlayable next,
        float progress)
    {
        targetMixer.SetInputWeight(
            0,
            current.IsValid() ? 1f - progress : 0f);
        targetMixer.SetInputWeight(1, next.IsValid() ? progress : 0f);
    }

    static void ResetMixerWeights(AnimationMixerPlayable targetMixer)
    {
        targetMixer.SetInputWeight(0, 0f);
        targetMixer.SetInputWeight(1, 0f);
    }

    void UpdateAuxiliaryLayerWeight()
    {
        bool hasAuxiliary = currentAuxiliaryPlayable.IsValid()
                            || nextAuxiliaryPlayable.IsValid();
        layerMixer.SetInputWeight(1, hasAuxiliary ? 1f : 0f);
    }

    void DestroyPlayable(
        AnimationMixerPlayable targetMixer,
        ref AnimationClipPlayable playable,
        int inputIndex)
    {
        transitionPlayables.Remove(playable);
        if (!playable.IsValid() || !graph.IsValid())
        {
            playable = default;
            return;
        }

        targetMixer.DisconnectInput(inputIndex);
        graph.DestroyPlayable(playable);
        playable = default;
    }

    public async UniTask WaitForClipEndAsync(
        AnimationClip clip,
        CancellationToken cancellationToken)
    {
        await WaitForClipTransitionAsync(clip, 0f, cancellationToken);
    }

    internal async UniTask WaitForClipTransitionAsync(
        AnimationClip clip,
        float transitionLeadTime,
        CancellationToken cancellationToken)
    {
        if (clip == null || !graph.IsValid())
        {
            return;
        }

        AnimationClipPlayable observedPlayable = isBlending
            ? nextBodyPlayable
            : currentBodyPlayable;
        if (!observedPlayable.IsValid())
        {
            return;
        }

        double transitionTime = Mathf.Max(
            0f,
            clip.length - Mathf.Max(0f, transitionLeadTime));
        await UniTask.WaitUntil(
            () => !observedPlayable.IsValid()
                  || observedPlayable.GetTime() >= transitionTime,
            cancellationToken: cancellationToken);
    }

    internal async UniTask WaitForLayeredPhaseEndAsync(
        AnimationClip bodyClip,
        AnimationClip auxiliaryClip,
        CancellationToken cancellationToken)
    {
        AnimationClip driverClip = bodyClip != null ? bodyClip : auxiliaryClip;
        AnimationClipPlayable observedPlayable = bodyClip != null
            ? currentBodyPlayable
            : currentAuxiliaryPlayable;
        if (driverClip == null || !observedPlayable.IsValid())
        {
            return;
        }

        await UniTask.WaitUntil(
            () => !observedPlayable.IsValid()
                  || observedPlayable.GetTime() >= driverClip.length,
            cancellationToken: cancellationToken);
    }

    internal async UniTask WaitForLayeredPhaseTimeAsync(
        AnimationClip bodyClip,
        AnimationClip auxiliaryClip,
        float phaseTime,
        CancellationToken cancellationToken)
    {
        AnimationClip driverClip = bodyClip != null ? bodyClip : auxiliaryClip;
        AnimationClipPlayable observedPlayable = bodyClip != null
            ? (isBlending ? nextBodyPlayable : currentBodyPlayable)
            : (isBlending ? nextAuxiliaryPlayable : currentAuxiliaryPlayable);
        if (driverClip == null || !observedPlayable.IsValid())
        {
            return;
        }

        double targetTime = Mathf.Clamp(phaseTime, 0f, driverClip.length);
        await UniTask.WaitUntil(
            () => !observedPlayable.IsValid()
                  || observedPlayable.GetTime() >= targetTime,
            cancellationToken: cancellationToken);
    }

    internal async UniTask WaitForLayeredBlendCompleteAsync(
        CancellationToken cancellationToken)
    {
        await UniTask.WaitUntil(
            () => !isBlending,
            cancellationToken: cancellationToken);
    }

    internal void PlayLayered(
        AnimationClip bodyClip,
        AnimationClip auxiliaryClip,
        bool crossFade,
        float blendDuration,
        float bodyStartTime = 0f,
        float auxiliaryStartTime = 0f,
        bool holdAtEnd = false)
    {
        if (animator == null || !graph.IsValid())
        {
            return;
        }

        if (crossFade)
        {
            CrossFadeLayeredWithPlayable(
                bodyClip,
                auxiliaryClip,
                Mathf.Max(0f, blendDuration),
                null,
                bodyStartTime,
                auxiliaryStartTime);
        }
        else
        {
            PlayImmediateLayeredWithPlayable(
                bodyClip,
                auxiliaryClip,
                bodyStartTime,
                auxiliaryStartTime);
        }

        if (holdAtEnd)
        {
            AnimationClipPlayable body = isBlending ? nextBodyPlayable : currentBodyPlayable;
            AnimationClipPlayable auxiliary = isBlending ? nextAuxiliaryPlayable : currentAuxiliaryPlayable;
            if (body.IsValid()) transitionPlayables.Add(body);
            if (auxiliary.IsValid()) transitionPlayables.Add(auxiliary);
        }
    }

    // AnimatorOverrideController有问题，先用这个
    public void CrossFadeTo(
        AnimationClip clip,
        float blendDuration,
        Action onBlendCompleted = null)
    {
        if (animator == null)
        {
            onBlendCompleted?.Invoke();
            return;
        }

        if (clip == null)
        {
            onBlendCompleted?.Invoke();
            return;
        }

        blendDuration = Mathf.Max(0f, blendDuration);

        if (graph.IsValid())
        {
            CrossFadeWithPlayable(clip, blendDuration, onBlendCompleted);
            return;
        }

        if (runtimeOverrideController == null)
        {
            onBlendCompleted?.Invoke();
            return;
        }

        // 乒乓切换：如果当前在A，就替换B的Clip并过渡到B；反之亦然。
        if (isPlayingStateA)
        {
            // 注意：这里的 "DefaultClipB" 必须是你 Animator Controller 中 StateB 默认绑定的 Clip 的真实名称
            runtimeOverrideController["biye"] = clip;
            animator.CrossFadeInFixedTime("StateB", blendDuration);
            Debug.Log("Switching to StateB: " + clip.name);
        }
        else
        {
            // 注意：这里的 "DefaultClipA" 必须是你 Animator Controller 中 StateA 默认绑定的 Clip 的真实名称
            runtimeOverrideController["idle"] = clip;
            animator.CrossFadeInFixedTime("StateA", blendDuration);
            Debug.Log("Switching to StateA: " + clip.name);
        }

        // 切换标记
        isPlayingStateA = !isPlayingStateA;

        if (completeRoutine != null)
        {
            StopCoroutine(completeRoutine);
        }

        completeRoutine = StartCoroutine(NotifyCompletedAfterDelay(blendDuration, onBlendCompleted));
    }
    
    //无过渡切换动作
    //用于初始化动作
    public void Play(AnimationClip clip, bool crossFade, float blendDuration)
    {
        if (crossFade)
        {
            CrossFadeTo(clip, blendDuration);
            return;
        }

        PlayImmediate(clip);
    }

    public void PlayImmediate(AnimationClip clip)
    {
        if (animator == null)
        {
            return;
        }

        if (completeRoutine != null)
        {
            StopCoroutine(completeRoutine);
            completeRoutine = null;
        }

        if (clip == null)
        {
            return;
        }

        if (graph.IsValid())
        {
            PlayImmediateWithPlayable(clip);
            return;
        }

        if (runtimeOverrideController == null)
        {
            return;
        }

        // 强制写入 override
        runtimeOverrideController["idle"] = clip;
        
        animator.Play("StateA", 0, 0f);

        // 防止第一次点击切换没有过渡
        isPlayingStateA = true; 
        animator.Update(0f);
    }

    IEnumerator NotifyCompletedAfterDelay(float delay, Action onBlendCompleted)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        completeRoutine = null;
        onBlendCompleted?.Invoke();
    }
    
    /*// 对外入口：应用Preset
    public void ApplyPreset(CameraPreset preset)
    {
        currentPreset = preset;

        var clip =preset.animationClip;
        if (clip == null) return;

        // 乒乓切换：如果当前在A，就替换B的Clip并过渡到B；反之亦然。
        if (isPlayingStateA)
        {
            // 注意：这里的 "DefaultClipB" 必须是你 Animator Controller 中 StateB 默认绑定的 Clip 的真实名称
            overrideController["biye"] = clip; 
            animator.CrossFade("StateB", preset.crossFadeDuration);
            Debug.Log("Switching to StateB: " + clip.name);
        }
        else
        {
            // 注意：这里的 "DefaultClipA" 必须是你 Animator Controller 中 StateA 默认绑定的 Clip 的真实名称
            overrideController["idle"] = clip; 
            animator.CrossFade("StateA", preset.crossFadeDuration);
            Debug.Log("Switching to StateA: " + clip.name);
        }

        // 切换标记
        isPlayingStateA = !isPlayingStateA;
        
        /#1#/ 停掉旧的随机循环
        if (idleRoutine != null)
            StopCoroutine(idleRoutine);

        // 立刻播放一个
        PlayRandomClip();

        // 开始循环
        idleRoutine = StartCoroutine(RandomIdleLoop());#1#
    }*/

    /*// 随机循环
    private IEnumerator RandomIdleLoop()
    {
        while (true)
        {
            float wait = Random.Range(currentPreset.minInterval, currentPreset.maxInterval);
            yield return new WaitForSeconds(wait);

            PlayRandomClip();
        }
    }

    // 播放随机动画
    private void PlayRandomClip()
    {
        var clip = GetRandomClip(currentPreset.animations);
        if (clip == null) return;

        // 替换 Idle 对应的动画
        overrideController["Idle"] = clip;

        // 平滑切换
        animator.CrossFade(STATE_NAME, currentPreset.crossFadeDuration);
    }

    // 权重随机
    private AnimationClip GetRandomClip(List<AnimationEntry> list)
    {
        if (list == null || list.Count == 0) return null;

        float total = 0f;
        foreach (var e in list)
            total += e.weight;

        float rand = Random.value * total;

        float sum = 0f;
        foreach (var e in list)
        {
            sum += e.weight;
            if (rand <= sum)
                return e.clip;
        }

        return list[0].clip;
    }*/
}
