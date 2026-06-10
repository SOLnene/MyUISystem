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
            { MainMenuAction.Team, () => ShowUnavailable(MainMenuAction.Team) },
            { MainMenuAction.Map, () => ShowUnavailable(MainMenuAction.Map) },
            { MainMenuAction.Quest, () => ShowUnavailable(MainMenuAction.Quest) },
            { MainMenuAction.Shop, () => ShowUnavailable(MainMenuAction.Shop) },
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
        var characterDefinition = GameDatabase.CharacterDatabase.Get("hutao");
        var characterModel = CharacterFactory.Create(characterDefinition, 1);
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
        Debug.LogWarning("Cannot open backpack: BackpackView is not registered in UIType/UIConfig yet.");
    }

    private void ShowUnavailable(MainMenuAction action)
    {
        Debug.LogWarning($"Main menu action has no UI entry yet: {action}");
    }
}
