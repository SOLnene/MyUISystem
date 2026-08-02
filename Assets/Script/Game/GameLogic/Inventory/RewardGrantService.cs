using System.Collections.Generic;

internal static class RewardGrantService
{
    internal static bool TryGrant(
        IReadOnlyList<RewardItemData> rewards)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return false;
        }

        ItemDefinition[] itemDefinitions = new ItemDefinition[rewards.Count];
        RewardItemData[] grantedRewards = new RewardItemData[rewards.Count];
        for (int i = 0; i < rewards.Count; i++)
        {
            RewardItemData reward = rewards[i];
            ItemDefinition itemDefinition =
                GameDatabase.ItemDatabase.GetItemByID(reward.ItemId);
            if (reward.Count <= 0 ||
                itemDefinition == null ||
                itemDefinition.category == ItemCategory.Equip &&
                itemDefinition is not EquipDefinition)
            {
                return false;
            }

            itemDefinitions[i] = itemDefinition;
            grantedRewards[i] = reward;
        }

        for (int i = 0; i < grantedRewards.Length; i++)
        {
            ItemGrantService.Grant(
                itemDefinitions[i],
                grantedRewards[i].Count);
        }

        GameSaveCoordinator.Instance.MarkDirty();
        EventBus<RewardGrantedEvent>.Raise(
            new RewardGrantedEvent(grantedRewards));
        return true;
    }
}
