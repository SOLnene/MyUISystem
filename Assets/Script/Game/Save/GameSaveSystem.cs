using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    const int CurrentVersion = 8;
    const string SaveFileName = "save.json";
    const string BackupFileName = "save.backup.json";
    const string TempFileName = "save.tmp";

    static string SaveDirectory
    {
        get
        {
            // 运行时数据属于已提交会话，保存目录也必须跟随同一个所有者。
            string sessionDirectory = GameContext.Instance.ActiveSaveDirectory;
            if (!string.IsNullOrEmpty(sessionDirectory))
            {
                return sessionDirectory;
            }

            return string.IsNullOrEmpty(SaveProfileManager.Instance.ActiveProfileDirectory)
                ? Application.persistentDataPath
                : SaveProfileManager.Instance.ActiveProfileDirectory;
        }
    }
    static string SavePath => Path.Combine(SaveDirectory, SaveFileName);
    static string BackupPath => Path.Combine(SaveDirectory, BackupFileName);
    static string TempPath => Path.Combine(SaveDirectory, TempFileName);
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
            context.TeamRepository,
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
            context.TeamRepository,
            context.GachaService,
            context.StorePurchaseService);
    }

    static bool TrySave(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository,
        TeamRepository teamRepository,
        GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        if (economy == null
            || inventoryRepository == null
            || characterRepository == null
            || teamRepository == null)
        {
            Debug.LogError("写入存档失败: 游戏数据尚未初始化");
            return false;
        }

        string source = GameContext.Instance.ActiveSaveDirectory;
        if (string.IsNullOrEmpty(source))
        {
            Debug.LogError("写入存档失败: 尚未选择本地或云端会话。");
            return false;
        }

        GameSaveData saveData = new GameSaveData
        {
            version = CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            currencies = economy.ExportSaveData(),
            // Character export repairs missing starters before the inventory snapshot is captured.
            characters = characterRepository.ExportSaveData(),
            inventory = inventoryRepository.ExportSaveData(),
            team = teamRepository.ExportSaveData(),
            gacha = gachaService != null ? gachaService.ExportSaveData() : new GachaSaveData(),
            store = storePurchaseService != null
                ? storePurchaseService.ExportSaveData()
                : new StorePurchaseSaveData(),
            achievements = AchievementProgressService.Instance.ExportSaveData(),
            Tutorial = TutorialProgressService.ExportSaveData()
        };

        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            if (!CloudGameSaveSession.IsCloudSource(source))
            {
                WriteSaveFile(json);
                NeedsResave = false;
                preserveBackupOnNextSave = false;
                return true;
            }
            CloudGameSaveSession.RequireSession(source).QueueSave(json);
            // 同步接口不能把入队当作保存成功；协调器等待云端确认后再清除 dirty。
            return false;
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
            SaveDirectory,
            GameEconomy.Instance,
            context.InventoryRepository,
            context.CharacterRepository,
            context.TeamRepository,
            context.GachaService,
            context.StorePurchaseService);
    }

    public static bool TryLoad(
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository)
    {
        SaveLoadResult result = Load(
            SaveDirectory,
            economy,
            inventoryRepository,
            characterRepository,
            null,
            null,
            null);
        return result == SaveLoadResult.Success || result == SaveLoadResult.RecoveredFromBackup;
    }

    internal static SaveLoadResult LoadFromDirectory(
        string saveDirectory,
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository,
        TeamRepository teamRepository,
        GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        // 候选档案尚未激活，因此加载不能依赖全局 ActiveProfile。
        return Load(
            saveDirectory,
            economy,
            inventoryRepository,
            characterRepository,
            teamRepository,
            gachaService,
            storePurchaseService);
    }

    static SaveLoadResult Load(
        string saveDirectory,
        GameEconomy economy,
        InventoryRepository inventoryRepository,
        CharacterRepository characterRepository,
        TeamRepository teamRepository,
        GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        NeedsResave = false;
        preserveBackupOnNextSave = false;
        if (CloudGameSaveSession.IsCloudSource(saveDirectory))
            return LoadCloudSave(CloudGameSaveSession.RequireSession(saveDirectory).Json,
                economy, inventoryRepository, characterRepository, teamRepository,
                gachaService, storePurchaseService);
        string savePath = Path.Combine(saveDirectory, SaveFileName);
        string backupPath = Path.Combine(saveDirectory, BackupFileName);
        bool hasMainSave = File.Exists(savePath);
        bool hasBackupSave = File.Exists(backupPath);
        if (!hasMainSave && !hasBackupSave)
        {
            return SaveLoadResult.NotFound;
        }

        SaveLoadResult mainFailure = SaveLoadResult.Corrupted;
        if (hasMainSave &&
            TryReadSaveData(savePath, out GameSaveData mainSaveData, out bool mainNeedsResave, out mainFailure) &&
            TryApplySaveData(
                mainSaveData,
                economy,
                inventoryRepository,
                characterRepository,
                teamRepository,
                gachaService,
                storePurchaseService))
        {
            NeedsResave = mainNeedsResave;
            return SaveLoadResult.Success;
        }

        SaveLoadResult backupFailure = SaveLoadResult.Corrupted;
        if (hasBackupSave &&
            TryReadSaveData(backupPath, out GameSaveData backupSaveData, out _, out backupFailure) &&
            TryApplySaveData(
                backupSaveData,
                economy,
                inventoryRepository,
                characterRepository,
                teamRepository,
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

    internal static void ConfirmCloudSave()
    {
        NeedsResave = false;
    }

    static SaveLoadResult LoadCloudSave(string json, GameEconomy economy,
        InventoryRepository inventoryRepository, CharacterRepository characterRepository,
        TeamRepository teamRepository, GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        if (json == null)
            return SaveLoadResult.NotFound;
        try
        {
            if (!json.Contains("\"version\""))
                return SaveLoadResult.Corrupted;
            var data = JsonUtility.FromJson<GameSaveData>(json);
            if (data == null)
                return SaveLoadResult.Corrupted;
            if (data.version <= 0 || data.version > CurrentVersion)
                return SaveLoadResult.UnsupportedVersion;
            int sourceVersion = data.version;
            bool needsResave = NormalizeSaveData(data, sourceVersion);
            if (!ValidateSaveData(data))
                return SaveLoadResult.Corrupted;
            if (needsResave)
                PreserveWeaponMigrationSnapshot(Path.Combine(Application.persistentDataPath,
                    "SaveMigrationBackups", "cloud-" + Uri.EscapeDataString(CloudGameSaveSession.Current.PlayerId)), json);
            if (!TryApplySaveData(data, economy,
                inventoryRepository, characterRepository, teamRepository, gachaService,
                storePurchaseService))
                return SaveLoadResult.Corrupted;
            NeedsResave = needsResave;
            return SaveLoadResult.Success;
        }
        catch (Exception exception)
        {
            Debug.LogError($"读取云存档失败: {exception.Message}");
            return SaveLoadResult.Corrupted;
        }
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

            int sourceVersion = saveData.version;
            needsResave = NormalizeSaveData(saveData, sourceVersion);
            if (!ValidateSaveData(saveData))
            {
                saveData = null;
                return false;
            }

            if (needsResave)
                PreserveWeaponMigrationSnapshot(Path.GetDirectoryName(path), json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"读取存档失败: {exception.Message}");
            saveData = null;
            return false;
        }
    }

    static bool NormalizeSaveData(GameSaveData saveData, int sourceVersion)
    {
        saveData.version = CurrentVersion;
        saveData.currencies ??= new CurrencySaveData();
        saveData.inventory ??= new InventorySaveData();
        saveData.inventory.stacks ??= new List<ItemStackSaveData>();
        saveData.inventory.equips ??= new List<EquipItemSaveData>();
        saveData.inventory.DiscoveredMaterialIds ??= new List<int>();
        saveData.inventory.UnseenMaterialIds ??= new List<int>();
        saveData.inventory.UnseenEquipInstanceIds ??= new List<long>();
        // 旧存档没有未查看状态：现有材料登记为已发现，避免升级后全部显示红点。
        if (sourceVersion < 6)
        {
            saveData.inventory.DiscoveredMaterialIds.Clear();
            saveData.inventory.UnseenMaterialIds.Clear();
            saveData.inventory.UnseenEquipInstanceIds.Clear();
            foreach (ItemStackSaveData stack in saveData.inventory.stacks)
            {
                if (stack == null)
                {
                    continue;
                }

                ItemDefinition itemDefinition =
                    GameDatabase.ItemDatabase.GetItemByID(stack.itemId);
                if (itemDefinition != null &&
                    itemDefinition.category == ItemCategory.Material &&
                    !saveData.inventory.DiscoveredMaterialIds.Contains(stack.itemId))
                {
                    saveData.inventory.DiscoveredMaterialIds.Add(stack.itemId);
                }
            }
        }
        saveData.characters ??= new CharacterRepositorySaveData();
        saveData.team ??= new TeamSaveData();
        saveData.team.members ??= new List<TeamMemberSaveData>();
        saveData.team.presets ??= new List<TeamPresetSaveData>();
        if (saveData.team.presets.Count == 0
            && (saveData.team.isInitialized || saveData.team.members.Count > 0))
        {
            saveData.team.presets.Add(new TeamPresetSaveData
            {
                presetIndex = 0,
                isInitialized = saveData.team.isInitialized,
                members = new List<TeamMemberSaveData>(saveData.team.members)
            });
            saveData.team.activePresetIndex = 0;
        }

        foreach (TeamPresetSaveData preset in saveData.team.presets)
        {
            if (preset != null)
            {
                preset.members ??= new List<TeamMemberSaveData>();
            }
        }

        saveData.gacha ??= new GachaSaveData();
        saveData.store ??= new StorePurchaseSaveData();
        saveData.achievements ??= new AchievementSaveData();
        saveData.achievements.progress ??= new List<AchievementProgressSaveData>();
        saveData.achievements.claimedIds ??= new List<string>();
        saveData.Tutorial ??= new TutorialSaveData();
        saveData.Tutorial.completedIds ??= new List<string>();
        if (sourceVersion < 8)
            MigrateRetiredWeapons(saveData.inventory);
        return EnsureSavedStarterWeapons(saveData) || sourceVersion < CurrentVersion;
    }

    static void MigrateRetiredWeapons(InventorySaveData inventory)
    {
        foreach (EquipItemSaveData weapon in inventory.equips)
        {
            if (weapon == null)
                continue;
            // Retired IDs stay reserved. Changing only the definition preserves instance progress and ownership.
            string replacement = weapon.itemId switch
            {
                200001 => "Claymore_Reasoning",
                200003 or 200004 or 200005 or 200007 or 200009 or 200010 or 200011 => "Claymore_Zephyrus",
                200006 or 200008 => "Claymore_Wolfmound",
                _ => null
            };
            if (replacement == null)
                continue;
            var definition = GameDatabase.ItemDatabase.GetItemByKey(replacement) as EquipDefinition;
            if (definition == null)
                throw new InvalidDataException($"Weapon migration target is missing: {replacement}");
            weapon.itemId = definition.id;
        }
    }

    static bool EnsureSavedStarterWeapons(GameSaveData saveData)
    {
        var weapons = new Dictionary<long, EquipItemSaveData>();
        long lastInstanceId = 0;
        foreach (EquipItemSaveData weapon in saveData.inventory.equips)
        {
            if (weapon == null)
                continue;
            lastInstanceId = Math.Max(lastInstanceId, weapon.instanceId);
            weapons.TryAdd(weapon.instanceId, weapon);
        }
        bool changed = false;
        if (saveData.characters.characters == null)
            return false;
        foreach (CharacterSaveData character in saveData.characters.characters)
        {
            if (character == null || string.IsNullOrEmpty(character.characterKey))
                continue;
            var definition = GameDatabase.CharacterDatabase.Get(character.characterKey);
            if (definition == null)
                continue;
            if (weapons.TryGetValue(character.equippedWeaponInstanceId, out var savedWeapon)
                && GameDatabase.ItemDatabase.GetItemByID(savedWeapon.itemId) is EquipDefinition equipped
                && equipped.weaponType == definition.weaponType)
                continue;

            var starter = CharacterFactory.GetStarterWeaponDefinition(definition.weaponType);
            var weapon = new EquipItemSaveData(checked(++lastInstanceId), starter.id, 1, 0, 0, 1);
            saveData.inventory.equips.Add(weapon);
            weapons.Add(weapon.instanceId, weapon);
            character.equippedWeaponInstanceId = weapon.instanceId;
            changed = true;
        }
        return changed;
    }

    static void PreserveWeaponMigrationSnapshot(string directory, string json)
    {
        using var hash = SHA256.Create();
        string fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "");
        string path = Path.Combine(directory, "save.before-weapon-migration-" + fingerprint + ".json");
        Directory.CreateDirectory(directory);
        if (File.Exists(path))
        {
            if (File.ReadAllText(path, Encoding.UTF8) != json)
                throw new IOException("The existing weapon migration backup does not match the original save.");
            return;
        }
        // This archive is immutable and independent of the rotating main/backup save pair.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(json);
        writer.Flush();
        stream.Flush(true);
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
        var eligibleUnseenEquipIds = new HashSet<long>();
        if (saveData.inventory.equips != null)
        {
            foreach (EquipItemSaveData equip in saveData.inventory.equips)
            {
                if (equip == null)
                {
                    return false;
                }

                EquipDefinition equipDefinition =
                    GameDatabase.ItemDatabase.GetItemByID(equip.itemId) as EquipDefinition;
                if (equip.instanceId <= 0 ||
                    equip.level <= 0 ||
                    equip.exp < 0 ||
                    equip.rank < 0 ||
                    equip.refinementLevel <= 0 ||
                    !equipIds.Add(equip.instanceId) ||
                    equipDefinition == null)
                {
                    return false;
                }

                if (equipDefinition.stars >= 4)
                {
                    eligibleUnseenEquipIds.Add(equip.instanceId);
                }
            }
        }

        var stackIds = new HashSet<int>();
        if (saveData.inventory.stacks != null)
        {
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

        var discoveredMaterialIds = new HashSet<int>();
        foreach (int itemId in saveData.inventory.DiscoveredMaterialIds)
        {
            ItemDefinition itemDefinition = GameDatabase.ItemDatabase.GetItemByID(itemId);
            if (itemDefinition == null ||
                itemDefinition.category != ItemCategory.Material ||
                !discoveredMaterialIds.Add(itemId))
            {
                return false;
            }
        }

        var unseenMaterialIds = new HashSet<int>();
        foreach (int itemId in saveData.inventory.UnseenMaterialIds)
        {
            if (!discoveredMaterialIds.Contains(itemId) ||
                !stackIds.Contains(itemId) ||
                !unseenMaterialIds.Add(itemId))
            {
                return false;
            }
        }

        var unseenEquipInstanceIds = new HashSet<long>();
        foreach (long instanceId in saveData.inventory.UnseenEquipInstanceIds)
        {
            if (!eligibleUnseenEquipIds.Contains(instanceId) ||
                !unseenEquipInstanceIds.Add(instanceId))
            {
                return false;
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

        if (saveData.team.activePresetIndex < 0
            || saveData.team.activePresetIndex >= TeamRepository.PresetCount)
        {
            return false;
        }

        var teamPresetIndices = new HashSet<int>();
        foreach (TeamPresetSaveData preset in saveData.team.presets)
        {
            if (preset == null
                || preset.presetIndex < 0
                || preset.presetIndex >= TeamRepository.PresetCount
                || !teamPresetIndices.Add(preset.presetIndex)
                || (!preset.isInitialized && preset.members.Count > 0)
                || !ValidateTeamMembers(preset.members, characterKeys))
            {
                return false;
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

        var completedTutorialIds = new HashSet<string>();
        foreach (string tutorialId in saveData.Tutorial.completedIds)
        {
            if (string.IsNullOrWhiteSpace(tutorialId)
                || !completedTutorialIds.Add(tutorialId))
            {
                return false;
            }
        }

        return true;
    }

    static bool ValidateTeamMembers(
        IReadOnlyList<TeamMemberSaveData> members,
        HashSet<string> characterKeys)
    {
        var teamSlots = new HashSet<int>();
        var teamCharacterKeys = new HashSet<string>();
        for (int index = 0; index < members.Count; index++)
        {
            TeamMemberSaveData member = members[index];
            if (member == null
                || member.slotIndex < 0
                || member.slotIndex >= TeamRepository.MemberCapacity
                || string.IsNullOrEmpty(member.characterKey)
                || !teamSlots.Add(member.slotIndex)
                || !teamCharacterKeys.Add(member.characterKey)
                || !characterKeys.Contains(member.characterKey))
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
        TeamRepository teamRepository,
        GachaService gachaService,
        StorePurchaseService storePurchaseService)
    {
        try
        {
            inventoryRepository.ImportSaveData(saveData.inventory);
            characterRepository.ImportSaveData(saveData.characters, inventoryRepository);
            teamRepository?.ImportSaveData(saveData.team);
            gachaService?.ImportSaveData(saveData.gacha);
            storePurchaseService?.ImportSaveData(saveData.store);
            economy.ImportSaveData(saveData.currencies);
            AchievementProgressService.Instance.ImportSaveData(saveData.achievements);
            TutorialProgressService.ImportSaveData(saveData.Tutorial);
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
        Directory.CreateDirectory(SaveDirectory);

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
