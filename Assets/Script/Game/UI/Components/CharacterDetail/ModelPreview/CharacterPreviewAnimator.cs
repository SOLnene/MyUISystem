using System;
using System.Collections;
using System.Collections.Generic;
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
      private AnimationMixerPlayable mixer;
    
      private AnimationClipPlayable currentPlayable;
      private AnimationClipPlayable nextPlayable;
      private float blendTime;
      private float blendTimer;
      private bool isBlending;

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
        runtimeOverrideController = Instantiate(overrideController);
        animator.runtimeAnimatorController = runtimeOverrideController;
        animator.applyRootMotion = false; // 防止飞走
        isPlayingStateA = true;
    }

    public void Unbind()
    {
        StopActiveRoutines();
        if (animator != null)
        {
            animator.runtimeAnimatorController = originalAnimatorController;
            animator.applyRootMotion = originalApplyRootMotion;
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
    // AnimatorOverrideController有问题，先用这个
    public void CrossFadeTo(
        AnimationClip clip,
        float blendDuration,
        Action onBlendCompleted = null)
    {
        if (animator == null || runtimeOverrideController == null)
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
    public void PlayImmediate(AnimationClip clip)
    {
        if (animator == null || runtimeOverrideController == null)
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
