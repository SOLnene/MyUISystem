using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public abstract class UIMotionBase : MonoBehaviour
{
    bool isPlaying;
    bool isShown;
    bool playingTargetShown;
    int requestVersion;
    CancellationTokenSource motionCts;
    protected Sequence seq;
    public UniTask PlayEnter(bool instant = false)
    {
        return PlayToState(true, instant,true);
    }

    public UniTask PlayExit(bool instant = false)
    {
        return PlayToState(false, instant,false);
    }

    async UniTask PlayToState(bool shown, bool instant, bool replay)
    {
        if (isPlaying == false && isShown == shown && replay == false)
        {
            return;
        }

        int version = ++requestVersion;

        StopCurrentAnimation();

        if (instant)
        {
            ApplyEndState(shown);
            isShown = shown;
            return;
        }

        if (shown && replay)
        {
            ApplyEndState(false);
        }

        motionCts = new CancellationTokenSource();
        isPlaying = true;
        playingTargetShown = shown;

        try
        {
            await PlayAnimation(shown, motionCts.Token);

            if (version != requestVersion)
            {
                return;
            }

            ApplyEndState(shown);
            isShown = shown;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (version == requestVersion)
            {
                isPlaying = false;
            }
        }
    }

    /// <summary>
    /// 实际动画内容
    /// </summary>
    /// <param name="isEnter"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected abstract UniTask PlayAnimation(bool isEnter,CancellationToken token);
    
    protected abstract void ApplyEndState(bool isEnter);

    protected void StopCurrentAnimation()
    {
        motionCts?.Cancel();
        motionCts?.Dispose();
        motionCts = null;

        seq?.Kill();
        seq = null;

        isPlaying = false;
    }
    
    public void Cancel()
    {
        ++requestVersion;
        StopCurrentAnimation();
        ApplyEndState(false);
        isShown = false;
    }

    public virtual void Skip()
    {
        if (!isPlaying)
        {
            return;
        }
        ++requestVersion;
        StopCurrentAnimation();
        ApplyEndState(playingTargetShown);
        isShown = playingTargetShown;
    }
}
