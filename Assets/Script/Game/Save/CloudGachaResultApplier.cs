using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

internal static class CloudGachaResultApplier
{
    internal static GachaResult Apply(CloudGachaReply reply, InventoryRepository inventory,
        CharacterRepository characters, GachaService gacha, out int drawProgress)
    {
        var operation = reply.operation;
        if (reply.state.revision != operation.committedRevision)
            throw new InvalidOperationException("云端在本次抽卡后又有更新，请退出并重新登录以读取完整状态。");
        var save = JsonUtility.FromJson<GameSaveData>(reply.state.saveJson);
        // Settlement edits the version-8 layout; opaque cloud storage has no such upper-version limit.
        if (save == null || save.version != 8 || save.inventory?.equips == null ||
            save.characters?.characters == null || save.gacha == null || save.achievements?.progress == null)
            throw new InvalidDataException("服务器返回的抽卡存档结构无效，请重新登录。");
        var equips = new Dictionary<long, EquipItemSaveData>();
        foreach (var equip in save.inventory.equips)
            if (equip == null || equip.instanceId <= 0 || equip.instanceId == long.MaxValue ||
                !equips.TryAdd(equip.instanceId, equip))
                throw new InvalidDataException("服务器返回的武器实例无效。");
        var roles = new Dictionary<string, CharacterSaveData>(StringComparer.Ordinal);
        foreach (var role in save.characters.characters)
            if (role == null || string.IsNullOrWhiteSpace(role.characterKey) ||
                !roles.TryAdd(role.characterKey, role))
                throw new InvalidDataException("服务器返回的角色列表无效。");
        drawProgress = 0;
        foreach (var progress in save.achievements.progress)
            if (progress != null && progress.progressKey == AchievementProgressKeys.GachaDraw)
                drawProgress = progress.value;
        if (drawProgress < operation.count)
            throw new InvalidDataException("服务器返回的抽卡进度无效。");

        var requiredEquips = new HashSet<long>();
        var requiredRoles = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<GachaEntry>();
        var instanceIds = new List<long>();
        foreach (var award in operation.results)
        {
            if (award == null || string.IsNullOrWhiteSpace(award.entryKey) ||
                award.rarity < 1 || award.rarity > 5)
                throw new InvalidDataException("服务器返回的抽卡奖励无效。");
            if (award.entryType == (int)GachaEntryType.Equip)
            {
                if (!equips.TryGetValue(award.instanceId, out var equip) ||
                    GameDatabase.ItemDatabase.GetItemByID(equip.itemId) is not EquipDefinition definition ||
                    definition.key != award.entryKey || definition.stars != award.rarity ||
                    !requiredEquips.Add(award.instanceId))
                    throw new InvalidDataException("抽卡武器与当前客户端定义不一致。");
            }
            else if (award.entryType == (int)GachaEntryType.Character)
            {
                var definition = GameDatabase.CharacterDatabase.Get(award.entryKey);
                if (definition == null || definition.rarity != award.rarity ||
                    !roles.TryGetValue(award.entryKey, out var role) || role.talentTokenCount < 0)
                    throw new InvalidDataException("抽卡角色与当前客户端定义不一致。");
                var existing = characters.GetByKey(award.entryKey);
                if (existing != null && existing.TalentTokenCount > role.talentTokenCount)
                    throw new InvalidOperationException("角色命星在抽卡期间发生变化，请重新登录。");
                if (existing == null)
                {
                    if (!equips.TryGetValue(role.equippedWeaponInstanceId, out var starter) ||
                        GameDatabase.ItemDatabase.GetItemByID(starter.itemId) is not EquipDefinition starterDefinition ||
                        starterDefinition.weaponType != definition.weaponType)
                        throw new InvalidDataException("抽卡角色的初始武器无效。");
                    requiredEquips.Add(starter.instanceId);
                }
                requiredRoles.Add(award.entryKey);
            }
            else throw new InvalidDataException("服务器返回了不支持的抽卡奖励类型。");
            entries.Add(new GachaEntry
            {
                entryKey = award.entryKey, entryType = (GachaEntryType)award.entryType, rarity = award.rarity
            });
            instanceIds.Add(award.instanceId);
        }
        foreach (long id in requiredEquips)
        {
            var equip = equips[id];
            if (equip.level < 1 || equip.exp < 0 || equip.rank < 0 || equip.refinementLevel < 1 ||
                (inventory.TryGetItem(id, out var existing) && existing.Id != equip.itemId))
                throw new InvalidDataException("抽卡武器状态与当前背包冲突。");
        }

        // Validate the entire award set first, then update only awarded objects to preserve live UI bindings.
        foreach (long id in requiredEquips)
            inventory.ApplyGachaEquip(equips[id]);
        foreach (string key in requiredRoles)
            characters.ApplyGachaCharacter(roles[key]);
        gacha.ImportSaveData(save.gacha);
        return new GachaResult(entries, instanceIds);
    }
}
