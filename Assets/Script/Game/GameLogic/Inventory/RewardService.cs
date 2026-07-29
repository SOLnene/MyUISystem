internal static class RewardService
{
    internal static bool TryGrantAndShow(
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
        UIManager.Instance.Open(UIType.RewardPopupView, rewards);
        return true;
    }
}
