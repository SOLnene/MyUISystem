using System;
using UnityEngine;

internal interface ITutorialOverlaySession
{
    void Attach(TutorialOverlayView view);
    void Detach(TutorialOverlayView view);
#if UNITY_EDITOR
    void SkipForTesting();
#endif
}

internal enum TutorialExitReason
{
    Completed,
    Cancelled,
}

internal sealed class TutorialController : ITutorialOverlaySession, IDisposable
{
#if UNITY_EDITOR
    // 编辑器测试期间忽略完成记录，重新创建控制器即可重复执行教程。
    const bool AlwaysRunForTesting = true;
#else
    const bool AlwaysRunForTesting = false;
#endif

    readonly TutorialDefinition definition;
    readonly EventBinding<TutorialSignalEvent> signalBinding;
    readonly Action<TutorialController, TutorialExitReason> ended;

    TutorialOverlayView overlayView;
    int stepIndex = -1;
    bool started;
    bool subscribed;
    bool closeRequested;
    bool endedNotified;

    public TutorialController(
        TutorialDefinition definition,
        Action<TutorialController, TutorialExitReason> ended)
    {
        this.definition = definition != null
            ? definition
            : throw new ArgumentNullException(nameof(definition));
        this.ended = ended;
        signalBinding = new EventBinding<TutorialSignalEvent>(HandleSignal);
    }

    public bool TryStart()
    {
        if (started ||
            (!AlwaysRunForTesting &&
             TutorialProgressService.IsCompleted(definition.TutorialId)))
        {
            return false;
        }

        if (definition.Steps.Count == 0)
        {
            Debug.LogError($"Tutorial {definition.TutorialId} has no steps.");
            return false;
        }

        started = true;
        stepIndex = 0;
        UIManager.Instance.Open(UIType.TutorialOverlayView, this);
        return true;
    }

    public void Attach(TutorialOverlayView view)
    {
        overlayView = view;
        // View 可见后才消费行为信号，避免异步加载期间把尚未展示的步骤提前完成。
        Subscribe();
        // Overlay 异步打开前目标界面可能已经出现，需要先同步现场状态以免教程卡住。
        if (AdvancePastSatisfiedTargetSteps())
        {
            RefreshPresentation();
        }
    }

    public void Detach(TutorialOverlayView view)
    {
        if (overlayView != view)
        {
            return;
        }

        overlayView = null;
        if (closeRequested)
        {
            return;
        }

        // Overlay 被外部流程关闭时取消本次运行，不能在无界面的状态下继续消费事件。
        closeRequested = true;
        DisposeSubscriptions();
        NotifyEnded(TutorialExitReason.Cancelled);
    }

    void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        subscribed = true;
        TutorialTargetRegistry.Registered += HandleTargetRegistered;
        TutorialTargetRegistry.Unregistered += HandleTargetUnregistered;
        EventBus<TutorialSignalEvent>.Register(signalBinding);
    }

    void HandleTargetRegistered(TutorialTargetId targetId, RectTransform target)
    {
        if (!HasCurrentStep)
        {
            return;
        }

        TutorialStepData currentStep = CurrentStep;
        if (currentStep.CompletionMode == TutorialCompletionMode.TargetRegistered
            && currentStep.CompletionTargetId == targetId)
        {
            Advance();
            return;
        }

        if (currentStep.TargetId == targetId)
        {
            overlayView?.FocusTarget(target, currentStep.Message);
        }
    }

    void HandleTargetUnregistered(TutorialTargetId targetId, RectTransform target)
    {
        if (HasCurrentStep && targetId == CurrentStep.TargetId)
        {
            overlayView?.HideGuidance();
        }
    }

    void HandleSignal(TutorialSignalEvent signalEvent)
    {
        if (!HasCurrentStep)
        {
            return;
        }

        TutorialStepData currentStep = CurrentStep;
        if (currentStep.CompletionMode != TutorialCompletionMode.Signal
            || currentStep.CompletionSignalId != signalEvent.SignalId)
        {
            return;
        }

        Advance();
    }

    void Advance()
    {
        stepIndex++;
        if (AdvancePastSatisfiedTargetSteps())
        {
            RefreshPresentation();
        }
    }

    bool AdvancePastSatisfiedTargetSteps()
    {
        // TargetRegistered 表示导航结果；结果目标已经激活时，这一步应视为已经完成。
        while (HasCurrentStep)
        {
            TutorialStepData currentStep = CurrentStep;
            if (currentStep.CompletionMode != TutorialCompletionMode.TargetRegistered
                || currentStep.CompletionTargetId == TutorialTargetId.None
                || !TutorialTargetRegistry.TryGet(
                    currentStep.CompletionTargetId,
                    out _))
            {
                return true;
            }

            stepIndex++;
        }

        Complete();
        return false;
    }

    void RefreshPresentation()
    {
        if (overlayView == null)
        {
            return;
        }

        TutorialStepData currentStep = CurrentStep;
        TutorialTargetId targetId = currentStep.TargetId;
        if (targetId != TutorialTargetId.None
            && TutorialTargetRegistry.TryGet(targetId, out RectTransform target))
        {
            overlayView.FocusTarget(target, currentStep.Message);
            return;
        }

        if (targetId == TutorialTargetId.None)
        {
            overlayView.ShowMessageOnly(currentStep.Message);
            return;
        }

        overlayView.HideGuidance();
    }

    void Complete()
    {
        stepIndex = definition.Steps.Count;
        // 只有首次完成才触发存档，避免重复完成产生无意义的写盘。
        if (TutorialProgressService.Complete(definition.TutorialId))
        {
            GameSaveCoordinator.Instance.MarkDirty();
            GameSaveCoordinator.Instance.Flush();
        }

        RequestClose(TutorialExitReason.Completed);
    }

#if UNITY_EDITOR
    public void SkipForTesting()
    {
        if (!started || !HasCurrentStep)
        {
            return;
        }

        stepIndex = definition.Steps.Count;
        RequestClose(TutorialExitReason.Cancelled);
    }
#endif

    void RequestClose(TutorialExitReason reason)
    {
        if (closeRequested || endedNotified)
        {
            return;
        }

        closeRequested = true;
        DisposeSubscriptions();
        if (overlayView == null)
        {
            UIManager.Instance.Close(UIType.TutorialOverlayView);
            NotifyEnded(reason);
            return;
        }

        // Close callback 发生在 UIViewHandle 回到 Closed 后，调度器此时才能安全开启下一份教程。
        UIManager.Instance.Close(
            UIType.TutorialOverlayView,
            () => NotifyEnded(reason));
    }

    void NotifyEnded(TutorialExitReason reason)
    {
        if (endedNotified)
        {
            return;
        }

        endedNotified = true;
        ended?.Invoke(this, reason);
    }

    bool HasCurrentStep =>
        stepIndex >= 0 && stepIndex < definition.Steps.Count;

    TutorialStepData CurrentStep => definition.Steps[stepIndex];

    void DisposeSubscriptions()
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;
        TutorialTargetRegistry.Registered -= HandleTargetRegistered;
        TutorialTargetRegistry.Unregistered -= HandleTargetUnregistered;
        EventBus<TutorialSignalEvent>.Deregister(signalBinding);
    }

    public void Dispose()
    {
        DisposeSubscriptions();
        if (started && !closeRequested)
        {
            closeRequested = true;
            // Close 也会取消 UIViewHandle 尚未完成的异步加载，避免宿主释放后延迟打开 Overlay。
            UIManager.Instance.Close(UIType.TutorialOverlayView);
        }

        overlayView = null;
    }
}
