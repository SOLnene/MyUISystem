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
            { MainMenuAction.Friend, OpenFriend },
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

        if (characterModel == null || !ModelViewer.Instance.CanShowCharacter(characterModel.Definition.key))
        {
            characterModel = null;
            foreach (var character in characterRepository.Characters)
            {
                if (!ModelViewer.Instance.CanShowCharacter(character.Definition.key))
                    continue;

                characterModel = character;
                break;
            }
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
        UIManager.Instance.Open(
            UIType.AchievementView,
            null,
            () => AchievementProgressService.Instance.AddProgress(
                AchievementProgressKeys.AchievementViewOpen));
    }

    private void OpenFriend()
    {
        // 联机数据源接入前，在页面打开处提供可替换的固定测试数据。
        var friends = new[]
        {
            new FriendInfo(
                new PlayerSummaryInfo("旅行者", "100000001"),
                60,
                "在线",
                "今天也要认真冒险。"),
            new FriendInfo(
                new PlayerSummaryInfo("安柏", "100000002"),
                58,
                "在线",
                "侦察骑士，准备出发！"),
            new FriendInfo(
                new PlayerSummaryInfo("凯亚", "100000003"),
                55,
                "2分钟前",
                "偶尔也该认真工作一下。"),
            new FriendInfo(
                new PlayerSummaryInfo("诺艾尔", "100000004"),
                52,
                "30分钟前",
                "需要帮忙的话，请尽管告诉我。"),
            new FriendInfo(
                new PlayerSummaryInfo("香菱", "100000005"),
                50,
                "1小时前",
                "正在寻找新的食材！"),
            new FriendInfo(
                new PlayerSummaryInfo("行秋", "100000006"),
                48,
                "昨天",
                "偷得浮生半日闲。"),
            new FriendInfo(
                new PlayerSummaryInfo("砂糖", "100000007"),
                45,
                "离线",
                "实验记录还需要整理。")
        };

        UIManager.Instance.Open(
            UIType.FriendView,
            new FriendViewModel(friends, HandleFriendChatRequested));
    }

    private void HandleFriendChatRequested(string playerId)
    {
        Debug.Log($"请求打开与玩家 {playerId} 的聊天界面。");
    }

    private void ShowUnavailable(MainMenuAction action)
    {
        Debug.LogWarning($"Main menu action has no UI entry yet: {action}");
    }
}
