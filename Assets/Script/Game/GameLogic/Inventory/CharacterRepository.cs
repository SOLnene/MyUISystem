using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public class CharacterRepository
{
    readonly List<CharacterModel> characters = new List<CharacterModel>();
    readonly Dictionary<string, CharacterModel> characterMap = new Dictionary<string, CharacterModel>();

    public IReadOnlyList<CharacterModel> Characters => characters;

    public CharacterModel Add(CharacterDefinition definition)
    {
        if (characterMap.TryGetValue(definition.key, out CharacterModel existing))
        {
            return existing;
        }

        CharacterModel model = CharacterFactory.Create(definition, 1);
        characters.Add(model);
        characterMap.Add(definition.key, model);
        return model;
    }

    public CharacterModel GetByKey(string key)
    {
        characterMap.TryGetValue(key, out CharacterModel model);
        return model;
    }

    public CharacterRepositorySaveData ExportSaveData()
    {
        CharacterRepositorySaveData saveData = new CharacterRepositorySaveData();
        foreach (CharacterModel character in characters)
        {
            EquipItem equipItem = character.CurrentEquipRP.Value;
            long equippedWeaponInstanceId = equipItem != null ? equipItem.InstanceId : 0;
            saveData.characters.Add(new CharacterSaveData(
                character.Definition.key,
                character.LevelSystem.Level,
                character.LevelSystem.CurrentExp,
                character.RankSystem.CurrentRank,
                character.TalentLevelRP.Value,
                equippedWeaponInstanceId));
        }

        return saveData;
    }

    public void ImportSaveData(CharacterRepositorySaveData saveData, InventoryRepository inventoryRepository)
    {
        characters.Clear();
        characterMap.Clear();
        if (saveData == null || saveData.characters == null)
        {
            return;
        }

        foreach (CharacterSaveData characterData in saveData.characters)
        {
            ImportCharacter(characterData, inventoryRepository);
        }
    }

    void ImportCharacter(CharacterSaveData characterData, InventoryRepository inventoryRepository)
    {
        CharacterDefinition definition = GameDatabase.CharacterDatabase.Get(characterData.characterKey);
        if (definition == null)
        {
            Debug.LogWarning($"读取角色失败，找不到角色: {characterData.characterKey}");
            return;
        }

        CharacterModel model = CharacterFactory.Create(
            definition,
            characterData.level,
            characterData.exp,
            characterData.rank,
            characterData.talentLevel);
        if (characterData.equippedWeaponInstanceId > 0 &&
            inventoryRepository.TryGetEquip(characterData.equippedWeaponInstanceId, out EquipItem equipItem))
        {
            model.ChangeEquip(equipItem);
        }

        characters.Add(model);
        characterMap.Add(definition.key, model);
    }
}
