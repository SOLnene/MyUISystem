using System;
using System.Collections.Generic;
using Game.UI.Components.CharacterDetail;
using UnityEngine;

internal class MainMenuNavigator
{
    private readonly Dictionary<MainMenuAction, Action> handlers;

    public MainMenuNavigator()
    {
        handlers = new Dictionary<MainMenuAction, Action>
        {
            { MainMenuAction.Character, OpenCharacter },
            { MainMenuAction.Gacha, OpenGacha },
            { MainMenuAction.Weapon, OpenWeapon },
            { MainMenuAction.Backpack, OpenBackpack },
            { MainMenuAction.Team, OpenTeam },
            { MainMenuAction.Map, () => ShowUnavailable(MainMenuAction.Map) },
            { MainMenuAction.Quest, () => ShowUnavailable(MainMenuAction.Quest) },
            { MainMenuAction.Shop, OpenStore },
            { MainMenuAction.Achievement, OpenAchievement },
        };
    }

    public void Open(MainMenuAction action)
    {
        if (handlers.TryGetValue(action, out var handler))
        {
            handler.Invoke();
            return;
        }

        Debug.LogWarning($"Unhandled main menu action: {action}");
    }

    private void OpenCharacter()
    {
        var characterRepository = GameContext.Instance.CharacterRepository;

        if (characterRepository.Characters.Count == 0)
        {
            string[] testCharacterKeys = { "citlali", "columbina", "furina", "lumine" };
            bool addedCharacter = false;
            for (int i = 0; i < testCharacterKeys.Length; i++)
            {
                var testCharacterDefinition = GameDatabase.CharacterDatabase.Get(testCharacterKeys[i]);
                if (testCharacterDefinition != null)
                {
                    characterRepository.Add(testCharacterDefinition);
                    addedCharacter = true;
                }
            }

            if (addedCharacter)
            {
                GameSaveCoordinator.Instance.MarkDirty();
            }
        }

        var characterDefinition = GameDatabase.CharacterDatabase.Get("citlali");
        var characterModel = characterDefinition != null
            ? characterRepository.GetByKey(characterDefinition.key)
            : null;

        if (characterModel == null && characterRepository.Characters.Count > 0)
        {
            characterModel = characterRepository.Characters[0];
        }

        if (characterModel == null)
        {
            Debug.LogWarning("Cannot open character detail: no character model found.");
            return;
        }

        var viewModel = new CharacterDetailViewModel(characterModel);
        UIManager.Instance.Open(UIType.CharacterDetailView, viewModel);
    }

    private void OpenGacha()
    {
        UIManager.Instance.Open(UIType.GachaView);
    }

    private void OpenWeapon()
    {
        if (GameContext.Instance.InventoryRepository == null)
        {
            Debug.LogWarning("Cannot open weapon detail: inventory repository is not ready.");
            return;
        }

        foreach (var item in GameContext.Instance.InventoryRepository.GetAllItems())
        {
            if (item is EquipItem equipItem)
            {
                var weapon = new EquipItemViewModel(equipItem);
                UIManager.Instance.Open(UIType.EquipDetailView, new EquipDetailOpenParams(weapon, WeaponDetailTab.Info));
                return;
            }
        }

        Debug.LogWarning("Cannot open weapon detail: no weapon item found in inventory.");
    }

    private void OpenBackpack()
    {
        UIManager.Instance.Open(UIType.BackpackView);
    }

    private void OpenTeam()
    {
        UIManager.Instance.Open(UIType.TeamEditView);
    }

    private void OpenStore()
    {
        UIManager.Instance.Open(UIType.StoreView);
    }

    private void OpenAchievement()
    {
        UIManager.Instance.Open(UIType.AchievementView);
    }

    private void ShowUnavailable(MainMenuAction action)
    {
        Debug.LogWarning($"Main menu action has no UI entry yet: {action}");
    }
}
