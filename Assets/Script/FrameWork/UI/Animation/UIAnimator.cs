using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

//TODO:支持 ScriptableObject 配置库（不同 UI 复用同一套动画）。
//TODO:支持 Timeline 绑定（UI 动画和剧情同步）。
//TODO:支持 状态机模式（UI 动画作为状态切换的一部分）。
[ExecuteAlways]
public class UIAnimator : MonoBehaviour
{
    public List<UIAnimStep> sequences = new List<UIAnimStep>();
    private UIAnimStep currentUIAnimStep;

    public UnityEvent OnAnimationEnded;

    private Sequence doTweenSequence;

    public List<UIAnimGroup> groups = new List<UIAnimGroup>();
    
    //TODO:字典缓存sequence
    public void PlaySequence(string sequenceName = "",Action callback = null)
    {
        Kill();
        doTweenSequence = DOTween.Sequence();

        foreach (var seq in sequences)
        {
            if (!string.IsNullOrEmpty(sequenceName))
            {
                if (seq.name != sequenceName)
                {
                    continue;
                }
            }
            var stepTween = BuildStepSequence(seq);
            if (stepTween != null)
            {
                doTweenSequence.Insert(seq.StartTime, stepTween);
            }
            doTweenSequence.OnComplete(() =>
            {
                OnAnimationEnded?.Invoke();
                callback?.Invoke();
            });
        }

        doTweenSequence.OnComplete(() =>
        {
            OnAnimationEnded?.Invoke();
            callback?.Invoke();
        });
            
        doTweenSequence.Play();
    }

    public void PlayStep(UIAnimStep seq, Action callback = null)
    {
        Kill();
        doTweenSequence = DOTween.Sequence();
        var stepTween = BuildStepSequence(seq);
        if (stepTween != null)
        {
            doTweenSequence.Append(stepTween);
        }
        doTweenSequence.OnComplete(() =>
        {
            OnAnimationEnded?.Invoke();
            callback?.Invoke();
        });
        doTweenSequence.Play();
    }

    public void PlayGroup(string groupName = "", Action callback = null)
    {
        var group = groups.Find(g => g.groupName == groupName);
        if (group == null) return;

        Kill();
        doTweenSequence = DOTween.Sequence();

        foreach (var step in group.steps)
        {
            var tween = BuildStepSequence(step);
            if (tween == null) continue;

            if (group.isParallel)
                doTweenSequence.Join(tween);
            else
                doTweenSequence.Append(tween);
        }

        doTweenSequence.OnComplete(() =>
        {
            OnAnimationEnded?.Invoke();
            callback?.Invoke();
        }).Play();
    }
  
    
    public void Kill()
    {
        doTweenSequence?.Kill();
    }
    
    
    private Tween BuildStepSequence(UIAnimStep step)
    {
        if (step.Target == null && step.Type == UIAnimStep.SequenceType.Animation) return null;

        Tween tween = null;
        var rt = step.Target?.GetComponent<RectTransform>();

        switch (step.Type)
        {
            case UIAnimStep.SequenceType.Animation:
                tween = BuildAnimStep(step);
                break;

            case UIAnimStep.SequenceType.Wait:
                tween = DOVirtual.DelayedCall(step.Duration, () => { });
                break;

            case UIAnimStep.SequenceType.SetActive:
                tween = DOVirtual.DelayedCall(0, () => step.Target?.SetActive(step.IsActivating));
                break;

            case UIAnimStep.SequenceType.SFX:
                tween = DOVirtual.DelayedCall(0, () =>
                {
                    if (step.SFXFile != null)
                        AudioSource.PlayClipAtPoint(step.SFXFile, Vector3.zero);
                    else
                        Debug.LogWarning("No SFX assigned");
                });
                break;

            case UIAnimStep.SequenceType.UnityEvent:
                tween = DOVirtual.DelayedCall(0, () => step.Event?.Invoke());
                break;

            case UIAnimStep.SequenceType.LoadScene:
                tween = DOVirtual.DelayedCall(0, () => SceneManager.LoadScene(step.SceneToLoad));
                break;
        }

        if (tween != null)
        {
            tween.OnComplete(() => step.Event.Invoke());
        }
        #if UNITY_EDITOR
        if (tween != null)
            tween.SetUpdate(UpdateType.Manual);
        #endif

        return tween;
    }

    
    public Sequence BuildAnimStep(UIAnimStep seq)
    {
        var sequence = DOTween.Sequence();
        var rt = seq.Target.GetComponent<RectTransform>();

        if (rt != null)
        {
            if (seq.AnimateAnchoredPosition)
                doTweenSequence.Insert(seq.StartTime, rt.DOAnchorPos(seq.AnchoredPositionEnd, seq.Duration).From(seq.AnchoredPositionStart).SetEase(seq.EaseFunction));
            if (seq.AnimateLocalScale)
                doTweenSequence.Insert(seq.StartTime, rt.DOScale(seq.LocalScaleEnd, seq.Duration).From(seq.LocalScaleStart).SetEase(seq.EaseFunction));
            if (seq.AnimateRotation)
                doTweenSequence.Insert(seq.StartTime, rt.DOLocalRotate(seq.RotationEnd, seq.Duration).From(seq.RotationStart).SetEase(seq.EaseFunction));
        }

        // Image 动画
        var img = seq.Target.GetComponent<Image>();
        if (img != null)
        {
            if (seq.AnimateColor)
                doTweenSequence.Insert(seq.StartTime, img.DOColor(seq.ColorEnd, seq.Duration).From(seq.ColorStart).SetEase(seq.EaseFunction));
            if (seq.AnimateFillAmount)
                doTweenSequence.Insert(seq.StartTime, img.DOFillAmount(seq.FillAmountEnd, seq.Duration).From(seq.FillAmountStart).SetEase(seq.EaseFunction));
        }

        // CanvasGroup 动画
        var cg = seq.Target.GetComponent<CanvasGroup>();
        if (cg != null && seq.AnimateAlpha)
            doTweenSequence.Insert(seq.StartTime, cg.DOFade(seq.AlphaEnd, seq.Duration).From(seq.AlphaStart).SetEase(seq.EaseFunction));

        // TextMeshPro 动画
        var tmp = seq.Target.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            if (seq.AnimateTMPColor)
                doTweenSequence.Insert(seq.StartTime, tmp.DOColor(seq.TMPColorEnd, seq.Duration).From(seq.TMPColorStart).SetEase(seq.EaseFunction));
            if (seq.AnimateMaxVisibleCharacters)
            {
                doTweenSequence.Insert(seq.StartTime, DOTween.To(() => tmp.maxVisibleCharacters, x => tmp.maxVisibleCharacters = x,
                    seq.MaxVisibleCharactersEnd, seq.Duration).From(seq.MaxVisibleCharactersStart).SetEase(seq.EaseFunction));
            }
        }
        #if UNITY_EDITOR
        sequence.SetUpdate(UpdateType.Manual);
        #endif
        return sequence;
    }
    
    

    #region 弃用代码
    //改用dotween自带控制
    /*public void PlaySequence(List<UIAnimStep> steps, int index, Action callback = null)
    {
        if (index >= steps.Count)
        {
            callback?.Invoke();
        }
        PlayStep(steps[index], () => PlaySequence(steps, index + 1, callback));
    }
    public void PlayParallel(List<UIAnimStep> steps,Action callback = null)
    {
        int completedCount = 0;
        foreach (var step in steps)
        {
            PlayStep(step, () =>
            {
                completedCount++;
                if (completedCount >= steps.Count)
                {
                    callback?.Invoke();
                }
            });
        }
    }*/
      #endregion
}
