using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public enum SaveLoadResult
{
    Success,
    NotFound,
    RecoveredFromBackup,
    UnsupportedVersion,
    Corrupted,
}

public static class GameSaveSystem
{
    const int CurrentVersion = 3;
    const string SaveFileName = "save.json";
    const string BackupFileName = "save.backup.json";
    const string TempFileName = "save.tmp";

    static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);
    static string TempPath => Path.Combine(Application.persistentDataPath, TempFileName);
    static bool preserveBackupOnNextSave;

    public static bool NeedsResave { get; private set; }

    public static void SaveCurrentGame()
    {
        TrySaveCurrentGame();
    }

    public static bool TrySaveCurrentGame()
    {
        GameContext context = GameContext.Instance;
        return TrySave(
            GameEconomy.Instance,
            context.InventoryRepository,
            context.CharacterRepository,
            context.GachaService,
            context.StorePurchaseService);
    }

    public static void Save(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository)
    {
        GameContext context = GameContext.Instance;
        TrySave(
            economy,
            inventoryRepository,
            characterRepository,
            context.GachaService,
            context.StorePurchaseService);
    }

    static bool TrySave(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository,
        GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        if (economy == null || inventoryRepository == null || characterRepository == null)
        {
            Debug.LogError("写入存档失败: 游戏数据尚未初始化");
            return false;
        }

        GameSaveData saveData = new GameSaveData
        {
            version = CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            currencies = economy.ExportSaveData(),
            inventory = inventoryRepository.ExportSaveData(),
            characters = characterRepository.ExportSaveData(),
            gacha = gachaService != null ? gachaService.ExportSaveData() : new GachaSaveData(),
            store = storePurchaseService != null
                ? storePurchaseService.ExportSaveData()
                : new StorePurchaseSaveData(),
            achievements = AchievementProgressService.Instance.ExportSaveData()
        };

        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            WriteSaveFile(json);
            NeedsResave = false;
            preserveBackupOnNextSave = false;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"写入存档失败: {exception.Message}");
            return false;
        }
    }

    public static bool TryLoadCurrentGame()
    {
        SaveLoadResult result = LoadCurrentGame();
        return result == SaveLoadResult.Success || result == SaveLoadResult.RecoveredFromBackup;
    }

    public static SaveLoadResult LoadCurrentGame()
    {
        GameContext context = GameContext.Instance;
        return Load(
            GameEconomy.Instance,
            context.InventoryRepository,
            context.CharacterRepository,
            context.GachaService,
            context.StorePurchaseService);
    }

    public static bool TryLoad(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository)
    {
        SaveLoadResult result = Load(
            economy,
            inventoryRepository,
            characterRepository,
            null,
            null);
        return result == SaveLoadResult.Success || result == SaveLoadResult.RecoveredFromBackup;
    }

    static SaveLoadResult Load(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository,
        GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        NeedsResave = false;
        preserveBackupOnNextSave = false;
        bool hasMainSave = File.Exists(SavePath);
        bool hasBackupSave = File.Exists(BackupPath);
        if (!hasMainSave && !hasBackupSave)
        {
            return SaveLoadResult.NotFound;
        }

        SaveLoadResult mainFailure = SaveLoadResult.Corrupted;
        if (hasMainSave &&
            TryReadSaveData(SavePath, out GameSaveData mainSaveData, out bool mainNeedsResave, out mainFailure) &&
            TryApplySaveData(
                mainSaveData,
                economy,
                inventoryRepository,
                characterRepository,
                gachaService,
                storePurchaseService))
        {
            NeedsResave = mainNeedsResave;
            return SaveLoadResult.Success;
        }

        SaveLoadResult backupFailure = SaveLoadResult.Corrupted;
        if (hasBackupSave &&
            TryReadSaveData(BackupPath, out GameSaveData backupSaveData, out _, out backupFailure) &&
            TryApplySaveData(
                backupSaveData,
                economy,
                inventoryRepository,
                characterRepository,
                gachaService,
                storePurchaseService))
        {
            NeedsResave = true;
            preserveBackupOnNextSave = true;
            Debug.LogWarning("主存档读取失败，已恢复备份存档");
            return SaveLoadResult.RecoveredFromBackup;
        }

        if (mainFailure == SaveLoadResult.UnsupportedVersion ||
            backupFailure == SaveLoadResult.UnsupportedVersion)
        {
            return SaveLoadResult.UnsupportedVersion;
        }

        return SaveLoadResult.Corrupted;
    }

    static bool TryReadSaveData(
        string path,
        out GameSaveData saveData,
        out bool needsResave,
        out SaveLoadResult failure)
    {
        saveData = null;
        needsResave = false;
        failure = SaveLoadResult.Corrupted;

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"version\""))
            {
                return false;
            }

            saveData = JsonUtility.FromJson<GameSaveData>(json);
            if (saveData == null)
            {
                return false;
            }

            if (saveData.version > CurrentVersion || saveData.version <= 0)
            {
                failure = SaveLoadResult.UnsupportedVersion;
                return false;
            }

            needsResave = saveData.version < CurrentVersion;
            NormalizeSaveData(saveData);
            if (!ValidateSaveData(saveData))
            {
                saveData = null;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"读取存档失败: {exception.Message}");
            saveData = null;
            return false;
        }
    }

    static void NormalizeSaveData(GameSaveData saveData)
    {
        saveData.version = CurrentVersion;
        saveData.currencies ??= new CurrencySaveData();
        saveData.inventory ??= new InventorySaveData();
        saveData.characters ??= new CharacterRepositorySaveData();
        saveData.gacha ??= new GachaSaveData();
        saveData.store ??= new StorePurchaseSaveData();
        saveData.achievements ??= new AchievementSaveData();
        saveData.achievements.progress ??= new List<AchievementProgressSaveData>();
        saveData.achievements.claimedIds ??= new List<string>();
    }

    static bool ValidateSaveData(GameSaveData saveData)
    {
        var currencyIds = new HashSet<int>();
        if (saveData.currencies.items != null)
        {
            foreach (CurrencyAmountSaveData item in saveData.currencies.items)
            {
                if (item == null || item.itemId <= 0 || item.amount < 0 || !currencyIds.Add(item.itemId))
                {
                    return false;
                }
            }
        }

        var equipIds = new HashSet<long>();
        if (saveData.inventory.equips != null)
        {
            foreach (EquipItemSaveData equip in saveData.inventory.equips)
            {
                if (equip == null ||
                    equip.instanceId <= 0 ||
                    equip.level <= 0 ||
                    equip.exp < 0 ||
                    equip.rank < 0 ||
                    equip.refinementLevel <= 0 ||
                    !equipIds.Add(equip.instanceId) ||
                    GameDatabase.ItemDatabase.GetItemByID(equip.itemId) is not EquipDefinition)
                {
                    return false;
                }
            }
        }

        if (saveData.inventory.stacks != null)
        {
            var stackIds = new HashSet<int>();
            foreach (ItemStackSaveData stack in saveData.inventory.stacks)
            {
                if (stack == null ||
                    stack.itemId <= 0 ||
                    stack.count <= 0 ||
                    !stackIds.Add(stack.itemId) ||
                    GameDatabase.ItemDatabase.GetItemByID(stack.itemId) == null)
                {
                    return false;
                }
            }
        }

        var characterKeys = new HashSet<string>();
        var equippedIds = new HashSet<long>();
        if (saveData.characters.characters != null)
        {
            foreach (CharacterSaveData character in saveData.characters.characters)
            {
                if (character == null ||
                    string.IsNullOrEmpty(character.characterKey) ||
                    character.level <= 0 ||
                    character.exp < 0 ||
                    character.rank < 0 ||
                    character.talentLevel < 0 ||
                    character.talentTokenCount < 0 ||
                    !characterKeys.Add(character.characterKey) ||
                    GameDatabase.CharacterDatabase.Get(character.characterKey) == null)
                {
                    return false;
                }

                if (character.equippedWeaponInstanceId > 0 &&
                    (!equipIds.Contains(character.equippedWeaponInstanceId) ||
                     !equippedIds.Add(character.equippedWeaponInstanceId)))
                {
                    return false;
                }
            }
        }

        var gachaKeys = new HashSet<string>();
        if (saveData.gacha.pityCounters != null)
        {
            foreach (GachaPitySaveData pity in saveData.gacha.pityCounters)
            {
                if (pity == null ||
                    string.IsNullOrEmpty(pity.gachaKey) ||
                    pity.count < 0 ||
                    !gachaKeys.Add(pity.gachaKey))
                {
                    return false;
                }
            }
        }

        var storeItemIds = new HashSet<int>();
        if (saveData.store.records != null)
        {
            foreach (StorePurchaseRecordSaveData record in saveData.store.records)
            {
                if (record == null ||
                    record.storeItemId <= 0 ||
                    string.IsNullOrEmpty(record.periodKey) ||
                    record.purchasedCount < 0 ||
                    !storeItemIds.Add(record.storeItemId))
                {
                    return false;
                }
            }
        }

        var achievementProgressKeys = new HashSet<string>();
        foreach (AchievementProgressSaveData progress in saveData.achievements.progress)
        {
            if (progress == null ||
                string.IsNullOrWhiteSpace(progress.progressKey) ||
                progress.value < 0 ||
                !achievementProgressKeys.Add(progress.progressKey))
            {
                return false;
            }
        }

        var claimedAchievementIds = new HashSet<string>();
        foreach (string achievementId in saveData.achievements.claimedIds)
        {
            if (string.IsNullOrWhiteSpace(achievementId) ||
                !claimedAchievementIds.Add(achievementId))
            {
                return false;
            }
        }

        return true;
    }

    static bool TryApplySaveData(
        GameSaveData saveData,
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository,
        GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        try
        {
            economy.ImportSaveData(saveData.currencies);
            inventoryRepository.ImportSaveData(saveData.inventory);
            characterRepository.ImportSaveData(saveData.characters, inventoryRepository);
            gachaService?.ImportSaveData(saveData.gacha);
            storePurchaseService?.ImportSaveData(saveData.store);
            AchievementProgressService.Instance.ImportSaveData(saveData.achievements);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"应用存档失败: {exception.Message}");
            return false;
        }
    }

    static void WriteSaveFile(string json)
    {
        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        if (!File.Exists(SavePath))
        {
            File.Move(TempPath, SavePath);
            return;
        }

        if (preserveBackupOnNextSave)
        {
            ReplaceMainSaveWithoutRotatingBackup();
            return;
        }

        if (File.Exists(BackupPath))
        {
            File.Delete(BackupPath);
        }

        try
        {
            File.Replace(TempPath, SavePath, BackupPath);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(SavePath, BackupPath, true);
            File.Delete(SavePath);
            File.Move(TempPath, SavePath);
        }
        catch (IOException)
        {
            File.Copy(SavePath, BackupPath, true);
            File.Delete(SavePath);
            File.Move(TempPath, SavePath);
        }
    }

    static void ReplaceMainSaveWithoutRotatingBackup()
    {
        try
        {
            File.Replace(TempPath, SavePath, null);
        }
        catch (PlatformNotSupportedException)
        {
            File.Delete(SavePath);
            File.Move(TempPath, SavePath);
        }
        catch (IOException)
        {
            File.Delete(SavePath);
            File.Move(TempPath, SavePath);
        }
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        if (File.Exists(BackupPath))
        {
            File.Delete(BackupPath);
        }

        if (File.Exists(TempPath))
        {
            File.Delete(TempPath);
        }
    }
}
