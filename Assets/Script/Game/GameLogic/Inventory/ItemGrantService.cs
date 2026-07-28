internal static class ItemGrantService
{
    internal static bool TryGrant(ItemDefinition itemDefinition, int amount)
    {
        if (itemDefinition == null || amount <= 0)
        {
            return false;
        }

        if (itemDefinition.category == ItemCategory.Currency)
        {
            GameEconomy.Instance.AddCurrency(itemDefinition.id, amount);
            return true;
        }

        InventoryRepository inventoryRepository =
            GameContext.Instance.InventoryRepository;
        switch (itemDefinition.category)
        {
            case ItemCategory.Consumable:
                inventoryRepository.AddItem(
                    new ConsumableItem(itemDefinition, amount));
                break;
            case ItemCategory.Material:
            case ItemCategory.ExpBook:
                inventoryRepository.AddItem(
                    new MaterialItem(itemDefinition, amount));
                break;
            case ItemCategory.Equip:
                if (itemDefinition is not EquipDefinition equipDefinition)
                {
                    return false;
                }

                for (int i = 0; i < amount; i++)
                {
                    inventoryRepository.AddItem(new EquipItem(equipDefinition));
                }

                break;
            default:
                for (int i = 0; i < amount; i++)
                {
                    inventoryRepository.AddItem(new InventoryItem(itemDefinition));
                }

                break;
        }

        return true;
    }
}
