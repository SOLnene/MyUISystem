using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
    public int version = 1;
    public CurrencySaveData currencies = new CurrencySaveData();
    public InventorySaveData inventory = new InventorySaveData();
    public CharacterRepositorySaveData characters = new CharacterRepositorySaveData();
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
    public long equippedWeaponInstanceId;

    public CharacterSaveData()
    {
    }

    public CharacterSaveData(string characterKey, int level, int exp, int rank, int talentLevel, long equippedWeaponInstanceId)
    {
        this.characterKey = characterKey;
        this.level = level;
        this.exp = exp;
        this.rank = rank;
        this.talentLevel = talentLevel;
        this.equippedWeaponInstanceId = equippedWeaponInstanceId;
    }
}
