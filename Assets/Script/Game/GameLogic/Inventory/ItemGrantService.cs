internal static class ItemGrantService
{
    internal static void Grant(ItemDefinition itemDefinition, int amount)
    {
        if (itemDefinition.category == ItemCategory.Currency)
        {
            GameEconomy.Instance.AddCurrency(itemDefinition.id, amount);
            return;
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
                EquipDefinition equipDefinition = (EquipDefinition)itemDefinition;

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
    }
}
