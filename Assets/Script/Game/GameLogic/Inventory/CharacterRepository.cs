using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public class CharacterRepository
{
    readonly List<CharacterModel> characters = new List<CharacterModel>();
    readonly Dictionary<string, CharacterModel> characterMap = new Dictionary<string, CharacterModel>();

    InventoryRepository inventoryRepository;

    public CharacterRepository() : this(new InventoryRepository())
    {
    }

    internal CharacterRepository(InventoryRepository inventoryRepository)
    {
        // Candidate sessions must register starters in their own inventory before being committed.
        this.inventoryRepository = inventoryRepository;
    }

    public IReadOnlyList<CharacterModel> Characters => characters;

    public CharacterModel Add(CharacterDefinition definition)
    {
        if (characterMap.TryGetValue(definition.key, out CharacterModel existing))
        {
            EnsureWeapon(existing);
            return existing;
        }

        CharacterModel model = CharacterFactory.Create(definition, 1);
        EnsureWeapon(model);
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
            EnsureWeapon(character);
            EquipItem equipItem = character.CurrentEquipRP.Value;
            long equippedWeaponInstanceId = equipItem != null ? equipItem.InstanceId : 0;
            saveData.characters.Add(new CharacterSaveData(
                character.Definition.key,
                character.LevelSystem.Level,
                character.LevelSystem.CurrentExp,
                character.RankSystem.CurrentRank,
                character.TalentLevelRP.Value,
                character.TalentTokenCountRP.Value,
                equippedWeaponInstanceId));
        }

        return saveData;
    }

    public void ImportSaveData(CharacterRepositorySaveData saveData, InventoryRepository inventoryRepository)
    {
        this.inventoryRepository = inventoryRepository;
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

        // Restored equipment is resolved before granting a starter, so valid saves never create extras.
        CharacterModel model = new CharacterModel(
            definition,
            characterData.level,
            characterData.exp,
            characterData.rank,
            characterData.talentLevel,
            characterData.talentTokenCount);
        if (characterData.equippedWeaponInstanceId > 0 &&
            inventoryRepository.TryGetEquip(characterData.equippedWeaponInstanceId, out EquipItem equipItem))
        {
            model.ChangeEquip(equipItem);
        }

        EnsureWeapon(model);
        characters.Add(model);
        characterMap.Add(definition.key, model);
    }

    void EnsureWeapon(CharacterModel model)
    {
        EquipItem weapon = model.CurrentEquipRP.Value;
        bool matchesType = weapon != null && weapon.EquipDefinition.weaponType == model.Definition.weaponType;
        if (matchesType && inventoryRepository.TryGetEquip(weapon.InstanceId, out EquipItem registered)
            && ReferenceEquals(weapon, registered))
            return;

        // Only a fresh factory starter can be registered directly; stale references receive a new starter.
        if (!matchesType || weapon.InstanceId > 0 || weapon.Stars != 3)
            weapon = new EquipItem(CharacterFactory.GetStarterWeaponDefinition(model.Definition.weaponType));
        inventoryRepository.AddItem(weapon);
        model.ChangeEquip(weapon);
    }
}
