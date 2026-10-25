using System;
using System.IO;
using System.Linq;
using UnityEngine;

internal static class CloudStoreResultApplier
{
    internal static void Apply(CloudStoreReply reply)
    {
        if (reply.state.revision != reply.operation.committedRevision)
            throw new InvalidOperationException("购买后云端已有新进度，请重新登录读取完整存档。");
        var save = JsonUtility.FromJson<GameSaveData>(reply.state.saveJson);
        if (save == null || save.version != 8 || save.currencies?.items == null || save.inventory?.stacks == null ||
            save.inventory.equips == null || save.store?.records == null || save.achievements?.progress == null ||
            save.currencies.items.Any(i => i == null || i.itemId <= 0 || i.amount < 0) ||
            save.currencies.items.Select(i => i.itemId).Distinct().Count() != save.currencies.items.Count)
            throw new InvalidDataException("购买存档结构无效，请重新登录恢复。");
        var operation = reply.operation;
        var definition = GameDatabase.ItemDatabase.GetItemByID(operation.itemId)
            ?? throw new InvalidDataException("客户端缺少购买物品定义。");
        var inventory = GameContext.Instance.InventoryRepository;
        if (definition.category == ItemCategory.Equip)
        {
            foreach (var equip in save.inventory.equips.Where(e => e.itemId == operation.itemId))
                inventory.ApplyGachaEquip(equip);
        }
        else if (definition.category != ItemCategory.Currency)
        {
            var stack = save.inventory.stacks.SingleOrDefault(s => s.itemId == operation.itemId);
            int delta = (stack?.count ?? 0) - inventory.GetItemCount(operation.itemId);
            if (delta < 0 || delta > operation.grantedAmount)
                throw new InvalidDataException("购买物品数量与本地进度冲突，请重新登录。");
            if (delta > 0) ItemGrantService.Grant(definition, delta);
        }
        GameEconomy.Instance.ApplyStoreBalances(save.currencies);
        GameContext.Instance.StorePurchaseService.ImportSaveData(save.store);
        var progress = save.achievements.progress.SingleOrDefault(p => p.progressKey == AchievementProgressKeys.StorePurchase);
        var achievements = AchievementProgressService.Instance;
        achievements.AddProgress(AchievementProgressKeys.StorePurchase,
            Math.Max(0, (progress?.value ?? 0) - achievements.GetProgress(AchievementProgressKeys.StorePurchase)));
    }
}
