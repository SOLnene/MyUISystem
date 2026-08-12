using System;
using Game.UI.Components.CharacterDetail;

internal sealed class CharacterEnhanceTutorialSignalAdapter : IDisposable
{
    readonly EventBinding<CharacterQuickFillCompletedEvent> quickFillBinding;
    readonly EventBinding<CharacterEnhanceCompletedEvent> enhanceBinding;

    internal CharacterEnhanceTutorialSignalAdapter()
    {
        quickFillBinding = new EventBinding<CharacterQuickFillCompletedEvent>(
            HandleQuickFillCompleted);
        enhanceBinding = new EventBinding<CharacterEnhanceCompletedEvent>(
            HandleEnhanceCompleted);
        EventBus<CharacterQuickFillCompletedEvent>.Register(quickFillBinding);
        EventBus<CharacterEnhanceCompletedEvent>.Register(enhanceBinding);
    }

    void HandleQuickFillCompleted(CharacterQuickFillCompletedEvent completedEvent)
    {
        if (completedEvent.TotalExp > 0)
        {
            EventBus<TutorialSignalEvent>.Raise(
                new TutorialSignalEvent(
                    TutorialSignalId.CharacterQuickFillCompleted));
        }
    }

    void HandleEnhanceCompleted(CharacterEnhanceCompletedEvent completedEvent)
    {
        EventBus<TutorialSignalEvent>.Raise(
            new TutorialSignalEvent(
                TutorialSignalId.CharacterEnhanceCompleted));
    }

    public void Dispose()
    {
        EventBus<CharacterQuickFillCompletedEvent>.Deregister(quickFillBinding);
        EventBus<CharacterEnhanceCompletedEvent>.Deregister(enhanceBinding);
    }
}
