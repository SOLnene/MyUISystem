using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public int version;
    public string savedAtUtc;
    public CurrencySaveData currencies = new CurrencySaveData();
    public InventorySaveData inventory = new InventorySaveData();
    public CharacterRepositorySaveData characters = new CharacterRepositorySaveData();
    public TeamSaveData team = new TeamSaveData();
    public GachaSaveData gacha = new GachaSaveData();
    public StorePurchaseSaveData store = new StorePurchaseSaveData();
    public AchievementSaveData achievements = new AchievementSaveData();
    [SerializeField] TutorialSaveData tutorial = new TutorialSaveData();

    internal TutorialSaveData Tutorial
    {
        get => tutorial;
        set => tutorial = value;
    }
}

[Serializable]
internal class TutorialSaveData
{
    public List<string> completedIds = new List<string>();
}

[Serializable]
public class CurrencySaveData
{
    public List<CurrencyAmountSaveData> items = new List<CurrencyAmountSaveData>();
}

[Serializable]
public class CurrencyAmountSaveData
{
    public int itemId;
    public int amount;

    public CurrencyAmountSaveData()
    {
    }

    public CurrencyAmountSaveData(int itemId, int amount)
    {
        this.itemId = itemId;
        this.amount = amount;
    }
}

[Serializable]
public class InventorySaveData
{
    public List<ItemStackSaveData> stacks = new List<ItemStackSaveData>();
    public List<EquipItemSaveData> equips = new List<EquipItemSaveData>();
    [SerializeField] List<int> discoveredMaterialIds = new List<int>();
    [SerializeField] List<int> unseenMaterialIds = new List<int>();
    [SerializeField] List<long> unseenEquipInstanceIds = new List<long>();

    internal List<int> DiscoveredMaterialIds
    {
        get => discoveredMaterialIds;
        set => discoveredMaterialIds = value;
    }

    internal List<int> UnseenMaterialIds
    {
        get => unseenMaterialIds;
        set => unseenMaterialIds = value;
    }

    internal List<long> UnseenEquipInstanceIds
    {
        get => unseenEquipInstanceIds;
        set => unseenEquipInstanceIds = value;
    }
}

[Serializable]
public class ItemStackSaveData
{
    public int itemId;
    public int count;

    public ItemStackSaveData()
    {
    }

    public ItemStackSaveData(int itemId, int count)
    {
        this.itemId = itemId;
        this.count = count;
    }
}

[Serializable]
public class EquipItemSaveData
{
    public long instanceId;
    public int itemId;
    public int level;
    public int exp;
    public int rank;
    public int refinementLevel;

    public EquipItemSaveData()
    {
    }

    public EquipItemSaveData(long instanceId, int itemId, int level, int exp, int rank, int refinementLevel)
    {
        this.instanceId = instanceId;
        this.itemId = itemId;
        this.level = level;
        this.exp = exp;
        this.rank = rank;
        this.refinementLevel = refinementLevel;
    }
}

[Serializable]
public class CharacterRepositorySaveData
{
    public List<CharacterSaveData> characters = new List<CharacterSaveData>();
}

[Serializable]
public class CharacterSaveData
{
    public string characterKey;
    public int level;
    public int exp;
    public int rank;
    public int talentLevel;
    public int talentTokenCount;
    public long equippedWeaponInstanceId;

    public CharacterSaveData()
    {
    }

    public CharacterSaveData(string characterKey, int level, int exp, int rank, int talentLevel, int talentTokenCount, long equippedWeaponInstanceId)
    {
        this.characterKey = characterKey;
        this.level = level;
        this.exp = exp;
        this.rank = rank;
        this.talentLevel = talentLevel;
        this.talentTokenCount = talentTokenCount;
        this.equippedWeaponInstanceId = equippedWeaponInstanceId;
    }
}

[Serializable]
public class TeamSaveData
{
    public int activePresetIndex;
    public List<TeamPresetSaveData> presets = new List<TeamPresetSaveData>();
    public bool isInitialized;
    public List<TeamMemberSaveData> members = new List<TeamMemberSaveData>();
}

[Serializable]
public class TeamPresetSaveData
{
    public int presetIndex;
    public bool isInitialized;
    public List<TeamMemberSaveData> members = new List<TeamMemberSaveData>();
}

[Serializable]
public class TeamMemberSaveData
{
    public int slotIndex;
    public string characterKey;

    public TeamMemberSaveData()
    {
    }

    public TeamMemberSaveData(int slotIndex, string characterKey)
    {
        this.slotIndex = slotIndex;
        this.characterKey = characterKey;
    }
}

[Serializable]
public class GachaSaveData
{
    public List<GachaPitySaveData> pityCounters = new List<GachaPitySaveData>();
}

[Serializable]
public class GachaPitySaveData
{
    public string gachaKey;
    public int count;

    public GachaPitySaveData()
    {
    }

    public GachaPitySaveData(string gachaKey, int count)
    {
        this.gachaKey = gachaKey;
        this.count = count;
    }
}

[Serializable]
public class StorePurchaseSaveData
{
    public List<StorePurchaseRecordSaveData> records = new List<StorePurchaseRecordSaveData>();
}

[Serializable]
public class StorePurchaseRecordSaveData
{
    public int storeItemId;
    public string periodKey;
    public int purchasedCount;

    public StorePurchaseRecordSaveData()
    {
    }

    public StorePurchaseRecordSaveData(int storeItemId, string periodKey, int purchasedCount)
    {
        this.storeItemId = storeItemId;
        this.periodKey = periodKey;
        this.purchasedCount = purchasedCount;
    }
}

[Serializable]
public class AchievementSaveData
{
    public List<AchievementProgressSaveData> progress = new List<AchievementProgressSaveData>();
    public List<string> claimedIds = new List<string>();
}

[Serializable]
public class AchievementProgressSaveData
{
    public string progressKey;
    public int value;

    public AchievementProgressSaveData()
    {
    }

    public AchievementProgressSaveData(string progressKey, int value)
    {
        this.progressKey = progressKey;
        this.value = value;
    }
}
