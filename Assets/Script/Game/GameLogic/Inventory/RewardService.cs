internal static class RewardService
{
    internal static bool TryGrant(
        ItemDefinition itemDefinition,
        int amount)
    {
        if (!ItemGrantService.TryGrant(itemDefinition, amount))
        {
            return false;
        }

        RewardItemData[] rewards =
        {
            new(itemDefinition.id, amount)
        };
        EventBus<RewardGrantedEvent>.Raise(new RewardGrantedEvent(rewards));
        return true;
    }
}
