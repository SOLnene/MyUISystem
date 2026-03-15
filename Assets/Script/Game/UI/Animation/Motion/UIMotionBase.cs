using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public abstract class UIMotionBase : BindableUI
{
    bool isPlaying;
    CancellationTokenSource motionCts;
    protected Sequence seq;
    public async UniTask PlayEnter(bool instant = false)
    {
        if (isPlaying)
        {
            return;
        }
        Cancel();
        motionCts = new CancellationTokenSource();
        var token = motionCts.Token;

        isPlaying = true;
        try
        {
            await PlayAnimation(true, token);
        }
        catch (OperationCanceledException)
        {
            //跳过
        }
        finally
        {
            isPlaying = false;
        }
    }

    /*public UniTask PlayExit(bool instant = false)
    {
        return PlayInternal(false, instant);
    }

    async UniTask PlayInternal(bool isEnter, bool instant)
    {
        if (isPlaying)
        {
            Skip();
        }
        isPlaying = true;

        if (instant)
        {
            ApplyEndState(isEnter);
        }
        else
        {
            await PlayAnimation(isEnter);
        }
        isPlaying = false;
    }*/

    /// <summary>
    /// 实际动画内容
    /// </summary>
    /// <param name="isEnter"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected abstract UniTask PlayAnimation(bool isEnter,CancellationToken token);
    
    protected abstract void ApplyIdleState();
    protected abstract void ApplyEndState(bool isEnter);

    public void Cancel()
    {
        motionCts?.Cancel();
        seq?.Kill();
        ApplyIdleState();
        isPlaying = false;
    }

    public virtual void Skip()
    {
        if (!isPlaying)
        {
            return;
        }
        seq.Kill(true);
        ApplyEndState(true);
        isPlaying = false;
    }
}
