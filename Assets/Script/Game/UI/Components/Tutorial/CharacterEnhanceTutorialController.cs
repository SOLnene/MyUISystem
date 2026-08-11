using System;
using Game.UI.Components.CharacterDetail;
using UnityEngine;

internal interface ITutorialOverlaySession
{
    void Attach(TutorialOverlayView view);
    void Detach(TutorialOverlayView view);
#if UNITY_EDITOR
    void SkipForTesting();
#endif
}

internal readonly struct CharacterQuickFillCompletedEvent : IEvent
{
    public readonly int TotalExp;

    public CharacterQuickFillCompletedEvent(int totalExp)
    {
        TotalExp = totalExp;
    }
}

internal readonly struct CharacterEnhanceCompletedEvent : IEvent
{
    public readonly EnhanceResultData Result;

    public CharacterEnhanceCompletedEvent(EnhanceResultData result)
    {
        Result = result;
    }
}

internal sealed class CharacterEnhanceTutorialController : ITutorialOverlaySession, IDisposable
{
    const string TutorialId = "character_enhance_intro";
#if UNITY_EDITOR
    const bool AlwaysRunForTesting = true;
#else
    const bool AlwaysRunForTesting = false;
#endif

    enum Step
    {
        OpenMainMenu,
        SelectCharacter,
        OpenEnhance,
        QuickFill,
        ConfirmEnhance,
        Completed,
    }

    readonly EventBinding<CharacterQuickFillCompletedEvent> quickFillBinding;
    readonly EventBinding<CharacterEnhanceCompletedEvent> enhanceBinding;

    TutorialOverlayView overlayView;
    Step step;
    bool started;
    bool subscribed;

    public CharacterEnhanceTutorialController()
    {
        quickFillBinding = new EventBinding<CharacterQuickFillCompletedEvent>(HandleQuickFillCompleted);
        enhanceBinding = new EventBinding<CharacterEnhanceCompletedEvent>(HandleEnhanceCompleted);
    }

    public void TryStart()
    {
        if (started ||
            (!AlwaysRunForTesting && TutorialProgressService.IsCompleted(TutorialId)))
        {
            return;
        }

        started = true;
        step = Step.OpenMainMenu;
        Subscribe();
        UIManager.Instance.Open(UIType.TutorialOverlayView, this);
    }

    public void Attach(TutorialOverlayView view)
    {
        overlayView = view;
        RefreshPresentation();
    }

    public void Detach(TutorialOverlayView view)
    {
        if (overlayView == view)
        {
            overlayView = null;
        }
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
        EventBus<CharacterQuickFillCompletedEvent>.Register(quickFillBinding);
        EventBus<CharacterEnhanceCompletedEvent>.Register(enhanceBinding);
    }

    void HandleTargetRegistered(TutorialTargetId targetId, RectTransform target)
    {
        switch (step)
        {
            case Step.OpenMainMenu when targetId == TutorialTargetId.MainMenuCharacter:
                EnterStep(Step.SelectCharacter);
                break;
            case Step.SelectCharacter when targetId == TutorialTargetId.CharacterEnhanceEntry:
                EnterStep(Step.OpenEnhance);
                break;
            case Step.OpenEnhance when targetId == TutorialTargetId.CharacterQuickAdd:
                EnterStep(Step.QuickFill);
                break;
            default:
                if (targetId == GetTargetId(step))
                {
                    overlayView?.FocusTarget(target, GetMessage(step));
                }
                break;
        }
    }

    void HandleTargetUnregistered(TutorialTargetId targetId, RectTransform target)
    {
        if (targetId == GetTargetId(step))
        {
            overlayView?.HideGuidance();
        }
    }

    void HandleQuickFillCompleted(CharacterQuickFillCompletedEvent completedEvent)
    {
        if (step == Step.QuickFill && completedEvent.TotalExp > 0)
        {
            EnterStep(Step.ConfirmEnhance);
        }
    }

    void HandleEnhanceCompleted(CharacterEnhanceCompletedEvent completedEvent)
    {
        if (step != Step.ConfirmEnhance)
        {
            return;
        }

        Complete();
    }

    void EnterStep(Step nextStep)
    {
        step = nextStep;
        RefreshPresentation();
    }

    void RefreshPresentation()
    {
        if (overlayView == null)
        {
            return;
        }

        TutorialTargetId targetId = GetTargetId(step);
        if (targetId != TutorialTargetId.None
            && TutorialTargetRegistry.TryGet(targetId, out RectTransform target))
        {
            overlayView.FocusTarget(target, GetMessage(step));
            return;
        }

        if (step == Step.OpenMainMenu)
        {
            overlayView.ShowMessageOnly("按 Esc 打开主菜单。");
            return;
        }

        overlayView.HideGuidance();
    }

    void Complete()
    {
        step = Step.Completed;
        if (TutorialProgressService.Complete(TutorialId))
        {
            GameSaveCoordinator.Instance.MarkDirty();
            GameSaveCoordinator.Instance.Flush();
        }

        UIManager.Instance.Close(UIType.TutorialOverlayView);
        DisposeSubscriptions();
    }

#if UNITY_EDITOR
    public void SkipForTesting()
    {
        if (!started || step == Step.Completed)
        {
            return;
        }

        step = Step.Completed;
        UIManager.Instance.Close(UIType.TutorialOverlayView);
        DisposeSubscriptions();
    }
#endif

    static TutorialTargetId GetTargetId(Step currentStep)
    {
        return currentStep switch
        {
            Step.SelectCharacter => TutorialTargetId.MainMenuCharacter,
            Step.OpenEnhance => TutorialTargetId.CharacterEnhanceEntry,
            Step.QuickFill => TutorialTargetId.CharacterQuickAdd,
            Step.ConfirmEnhance => TutorialTargetId.CharacterUpgrade,
            _ => TutorialTargetId.None,
        };
    }

    static string GetMessage(Step currentStep)
    {
        return currentStep switch
        {
            Step.SelectCharacter => "点击「角色」，查看角色信息。",
            Step.OpenEnhance => "点击「升级」，进入角色强化界面。",
            Step.QuickFill => "点击「快速添加」，自动选择升级材料。",
            Step.ConfirmEnhance => "点击「升级」，提升角色等级。",
            _ => string.Empty,
        };
    }

    void DisposeSubscriptions()
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;
        TutorialTargetRegistry.Registered -= HandleTargetRegistered;
        TutorialTargetRegistry.Unregistered -= HandleTargetUnregistered;
        EventBus<CharacterQuickFillCompletedEvent>.Deregister(quickFillBinding);
        EventBus<CharacterEnhanceCompletedEvent>.Deregister(enhanceBinding);
    }

    public void Dispose()
    {
        DisposeSubscriptions();
        overlayView = null;
    }
}
