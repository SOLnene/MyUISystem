using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIAnimationManager : Singleton<UIAnimationManager>
{
    private readonly List<UIAnimator> animators = new List<UIAnimator>();

    public void RegisterAnimator(UIAnimator animator)
    {
        if (!animators.Contains(animator))
            animators.Add(animator);
    }

    public void UnregisterAnimator(UIAnimator animator)
    {
        animators.Remove(animator);
    }

    /// <summary>
    /// 播放所有 UIAnimator 中的某个动画
    /// </summary>
    public void PlayAll(string sequenceName)
    {
        foreach (var animator in animators)
        {
            animator.PlaySequence(sequenceName);
        }
    }

    /// <summary>
    /// 播放单个 UIAnimator 的动画
    /// </summary>
    public void Play(UIAnimator animator, string sequenceName)
    {
        animator?.PlaySequence(sequenceName);
    }
}
