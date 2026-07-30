using System.Collections.Generic;

internal readonly struct RewardGrantedEvent : IEvent
{
    internal IReadOnlyList<RewardItemData> Rewards { get; }

    internal RewardGrantedEvent(IReadOnlyList<RewardItemData> rewards)
    {
        Rewards = rewards;
    }
}

internal readonly struct RewardPopupClosedEvent : IEvent
{
}
