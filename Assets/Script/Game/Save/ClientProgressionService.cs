using System.Linq;
using Game.Domain.Character;
using Game.Domain.Enhance;

internal static class ClientProgressionService
{
    internal static bool TryEnhanceCharacter(CharacterModel character, IMaterialInput input)
    {
        var selected = input.Counts.Where(pair => pair.Value.Value > 0)
            .Select(pair => (Id: GameDatabase.ItemDatabase.GetItemByKey(pair.Key).id, Count: pair.Value.Value)).ToArray();
        var inventory = GameContext.Instance.InventoryRepository;
        if (selected.Length == 0 || selected.Any(book => inventory.GetItemCount(book.Id) < book.Count)) return false;
        var result = character.PreviewEnhancement(input.GetTotalExp());
        if (result.cappedExpGain <= 0 || !GameEconomy.Instance.TrySpendGold(input.GetTotalGold())) return false;
        // Capture selection before inventory notifications clamp the material input and update its preview.
        foreach (var book in selected) inventory.ApplyStackCount(book.Id, inventory.GetItemCount(book.Id) - book.Count);
        character.ApplyEnhancementState(result.finalLevel, result.finalExp);
        return true;
    }

    internal static bool TryPromote(PromotionParticipant participant)
    {
        if (!participant.IsOwned || participant.Rank >= 6 || participant.Level != participant.Model.GetCurrentMaxLevel()) return false;
        var inventory = GameContext.Instance.InventoryRepository;
        var materials = participant.Materials;
        if (materials.Any(material => inventory.GetItemCount(material.ItemId) < material.Count)) return false;
        int rank = participant.Rank, exp = participant.Character != null ? participant.Exp : 0;
        if (!GameEconomy.Instance.TrySpendGold(participant.GoldCost)) return false;
        foreach (var material in materials) inventory.ApplyStackCount(material.ItemId, inventory.GetItemCount(material.ItemId) - material.Count);
        // Publish the new rank only after its resource deductions are complete.
        participant.Apply(rank + 1, exp);
        return true;
    }
}
