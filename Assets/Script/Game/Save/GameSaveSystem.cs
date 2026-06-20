using System;
using System.IO;
using UnityEngine;

public static class GameSaveSystem
{
    const string SaveFileName = "save.json";

    static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static void SaveCurrentGame()
    {
        Save(
            GameEconomy.Instance,
            GameContext.Instance.InventoryRepository,
            GameContext.Instance.CharacterRepository);
    }

    public static void Save(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository)
    {
        GameSaveData saveData = new GameSaveData
        {
            currencies = economy.ExportSaveData(),
            inventory = inventoryRepository.ExportSaveData(),
            characters = characterRepository.ExportSaveData()
        };

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(SavePath, json);
    }

    public static bool TryLoadCurrentGame()
    {
        return TryLoad(
            GameEconomy.Instance,
            GameContext.Instance.InventoryRepository,
            GameContext.Instance.CharacterRepository);
    }

    public static bool TryLoad(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository)
    {
        if (!File.Exists(SavePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
            if (saveData == null)
            {
                return false;
            }

            economy.ImportSaveData(saveData.currencies);
            inventoryRepository.ImportSaveData(saveData.inventory);
            characterRepository.ImportSaveData(saveData.characters, inventoryRepository);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"读取存档失败: {exception.Message}");
            return false;
        }
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }
    }
}
