using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EquipItem : InventoryItem
{
    public int Level { get; private set; }
    public int RefinementLevel { get; private set; }
    
    public int CurrentExp { get; private set; }
    public int NextLevelExp { get; private set; } 
    
    public int Rank { get; private set; }
    
    
    public new EquipDefinition EquipDefinition => base.ItemDefinition as EquipDefinition;
    
    public EquipItem(EquipDefinition def, int level = 1, int refine = 1,int currentExp = 0 , int nextLevelExp = 1000) : base(def)
    {
        Level = level;
        RefinementLevel = refine;
        CurrentExp = currentExp;
        NextLevelExp = GetExpRequired();
    }


    public override string GetDisplayLevelText() => $"Lv.{Level}\n";

    /// <summary>
    /// 详情面板中的主要属性显示(第二部分)
    /// </summary>
    /// <returns></returns>
    public override string GetDisplayMainText() => $"暴击伤害\n<b>{GetCriticalDamage()}%</b>\n基础攻击力\n<b><size=150%>{GetAttack()}</size></b>";

    public string GetDisplayMainStatText() => $"{GetAttack()}";
    
    public string GetDisplaySubStatText() => $"{GetCriticalDamage()}%";
    
    public string GetDisplayExpText() => $"{CurrentExp}/{NextLevelExp}%";
    public int GetAttack(int level = 0)
    {
        var lv = level == 0 ? Level : level;
        // 基础攻击由武器定义决定
        int baseAttack = EquipDefinition.baseAttack; // e.g. 100~200
        // 等级成长，简单线性或略微非线性
        float levelMultiplier = 1 + 0.05f * (lv - 1); // 每级增加5%
        // 精炼加成，按星级/精炼等级加固定百分比
        float refineMultiplier = 1 + 0.02f * RefinementLevel; // 每级精炼增加2%
    
        return Mathf.RoundToInt(baseAttack * levelMultiplier * refineMultiplier);
    }

    public float GetCriticalDamage(int level = 0)
    {
        var lv = level == 0 ? Level : level;
        // 基础暴伤：0.5 = 50%
        float baseCritDamage = EquipDefinition.baseCritDamage;

        // 精炼加成，每级精炼增加 5%
        float refineBonus = 0.05f * RefinementLevel;

        // 星级加成，每颗星增加 10%
        float starBonus = 0.1f * Stars;

        return baseCritDamage + refineBonus + starBonus;
    }

    /// <summary>
    /// 获取升级到目标等级所需经验
    /// </summary>
    public int GetExpRequired(int level = 0)
    {
        var Level = level == 0 ? this.Level : level;
        if (Level <= 1) return 0;
        
        // 基础参数
        const float baseExp = 100f;      // 初始经验需求
        const float growth = 1.45f;      // 成长系数（可按稀有度调整）

        // 经验需求公式
        float exp = baseExp * Mathf.Pow(Level, growth);

        // 根据稀有度放大倍数
        float rarityMultiplier = 1f + (int)ItemRarity * 0.3f; // 稀有度越高需求越多

        return Mathf.RoundToInt(exp * rarityMultiplier);
    }
    
    public int GetEnhanceCost(int gainedExp)
    {
        int baseEnhanceCost = 100;  // 每点经验基础金币消耗
        float rarityMultiplier = 0.5f + 0.5f * ((int)ItemRarity + 1);
        return Mathf.RoundToInt(baseEnhanceCost * rarityMultiplier * (1f + 0.1f * Level) * gainedExp);
    }
    
    /// <summary>
    /// 获取该装备作为素材时能提供的经验值
    /// 稀有度越高，提供经验越多
    /// </summary>
    public int GetExpValue()
    {
        // baseExp = 50，稀有度和等级加成
        float baseExp = 50f;
        float rarityMultiplier = 0.5f + 0.5f * ((int)ItemRarity + 1); // Common=1, Legendary=5
        float levelMultiplier = 1f + 0.1f * Level; // 每一级增加10%
    
        return Mathf.RoundToInt(baseExp * rarityMultiplier * levelMultiplier);
    }

    // 获取当前 rank 的最大等级
    public int GetMaxLevel()
    {
        return EquipDefinition.GetMaxLevelForRank(Rank);
    }

    // 是否可以突破（按当前等级和当前 rank）
    public bool CanBreakthrough()
    {
        int maxLevel = GetMaxLevel();
        return Level >= maxLevel && Rank < EquipDefinition.MaxRank;
    }

    /// <summary>
    /// 是否已经突破满了
    /// </summary>
    /// <returns></returns>
    public bool RankMaxed()
    {
        return Rank > EquipDefinition.MaxRank;
    }
    
    public int GetNextRankMaxLevel()
    {
        // 如果已经是最高Rank，则返回当前Rank的最大等级
        if (Rank >= EquipDefinition.MaxRank)
            return GetMaxLevel();

        var nextRank = Rank + 1;
        var nextInfo = EquipDefinition.GetRankInfo(nextRank);
        if (nextInfo == null)
            return GetMaxLevel();

        return nextInfo.maxLevel;
    }
    
    // 尝试突破：检查材料，通过 inventoryService 扣除材料，提升 Rank（返回是否成功）
    public bool TryBreakthrough()
    {
        if (!CanBreakthrough()) return false;

        var nextRank = Rank + 1;
        var req = EquipDefinition.GetRankInfo(nextRank);
        if (req == null) return false;

        /*// 检查所有材料
        foreach (var r in req.requirements)
        {
            if (!inventory.HasItems(r.materialKey, r.count))
                return false;
        }

        // 扣除材料
        foreach (var r in req.requirements)
        {
            if (!inventory.ConsumeItems(r.materialKey, r.count))
            {
                Debug.LogError("扣除材料失败（回滚未实现）");
                return false;
            }
        }*/

        // 扣金币或其他消耗你可以在这里做
        // 成功突破
        Rank = nextRank;
        CurrentExp = 0;
        //OnRankChanged?.Invoke(Rank);

        // 注意：突破后通常会把 level cap 提升，但不自动满级；保持当前 Level 不变
        return true;
    }

    public bool TryRefine()
    {
        if(RefinementLevel>=GetRefineCap()) return false;
        RefinementLevel++;
        return true;
    }

    
    public int GetRefineCost(int currentLevel)
    {
        int baseCost = 100;       // 等级1的基础花费
        float exponent = 1.5f;    // 指数增长
        return Mathf.RoundToInt(baseCost * Mathf.Pow(currentLevel, exponent));
    }
    
    public int GetRefineCap()
    {
        //默认为5
        return 5;
    }
    
    public EquipPreview GetPreviewWithExp(int addedExp)
    {
        // 创建一个临时副本（防止修改真实数据）
        int previewLevel = Level;
        int previewExp = CurrentExp + addedExp;
        int previewNextExp = NextLevelExp;
        int maxLevel = GetMaxLevel();
        int totalLevelUps = 0;
        // 模拟升级逻辑（与 AddExp 类似，但不真正改变状态）
        while (previewExp >= previewNextExp && previewLevel < maxLevel)
        {
            previewExp -= previewNextExp;
            previewLevel++;
            previewNextExp = GetExpRequired(previewLevel); // 注意：这里需要一个独立函数
            totalLevelUps++;
        }
        
        int cappedExpGained = (Level == previewLevel && previewExp > GetExpRequired(Level)) 
            ? GetExpRequired(Level) - CurrentExp 
            : addedExp; // 实际可以提升的经验，不超过 Rank 上限
       
        // 生成预览结构
        var preview = new EquipPreview
        {
            currentAtk = GetAttack(),
            nextAtk = GetAttack(previewLevel),
            currentCrit = GetCriticalDamage(),
            nextCrit = GetCriticalDamage(previewLevel),
            isBreakPreview = previewLevel >= maxLevel && CanBreakthrough(),
            levelUp = totalLevelUps,
            maxGainExp = cappedExpGained,
            costGold = GetEnhanceCost(cappedExpGained)
        };

        return preview;
    }
    
    public ExpGainResult AddExp(int exp)
    {
        if (RankMaxed())
            return ExpGainResult.MaxLevelReached;
    
        int maxLevel = GetMaxLevel();
        
        if (Level >= maxLevel)
            return ExpGainResult.RankLimitReached;
    
        CurrentExp += exp;
        ExpGainResult result = ExpGainResult.None;
        
        while (CurrentExp >= NextLevelExp)
        {
            if (Level >= maxLevel)
            {
                // 经验超出但受Rank限制
                CurrentExp = Mathf.Min(CurrentExp, NextLevelExp - 1);
                return ExpGainResult.RankLimitReached;
            }
    
            CurrentExp -= NextLevelExp;
            Level++;
            NextLevelExp = GetExpRequired();
            result = ExpGainResult.LeveledUp;
        }
    
        return result;
    }
    void LevelUp()
    {
        Level++;
        //CurrentExp = 0;
        NextLevelExp = GetExpRequired();
    }
}

// 单阶信息（类似“突破1：上限40，需要材料...，额外属性...”）
[Serializable]
public class RankInfo
{
    public int rank;             // 0 = 未突破（基础），1 = 突破一次 ...
    public int maxLevel;         // 该 rank 的等级上限 (例如 20,40,60...)
    public int goldCost;   
    public List<BreakthroughRequirement> requirements = new();
    // 可扩展：突破带来的固定属性加成或解锁（例如攻击力提升、精炼上限等）
    public float attackAddFlat = 0f;      // 额外固定攻击
    public float attackAddPercent = 0f;   // 额外百分比（如 +5%）
}
[Serializable]
public class BreakthroughRequirement
{
    public string materialKey; // 比如 "mat_enhance_1"
    public int count;

    public BreakthroughRequirement(string key, int c)
    {
        materialKey = key;
        count = c;
    }
}

public enum ExpGainResult
{
    None,             // 未发生变化
    LeveledUp,        // 升级了
    RankLimitReached, // 达到当前Rank的等级上限
    MaxLevelReached   // 已达最大等级（完全满级）
}

public struct EquipPreview
{
    public int currentAtk;
    public int nextAtk;
    public float currentCrit;
    public float nextCrit;
    public bool isBreakPreview;
    public int levelUp;
    // 新增字段：本次升级预览可获得的最大经验（不超过当前 Rank 上限）
    public int maxGainExp;
    public int costGold;
    public int AtkDiff => nextAtk - currentAtk;
    public float CritDiff => nextCrit - currentCrit;

    public override string ToString() =>
        $"ATK: {currentAtk} → {nextAtk} ({AtkDiff:+#;-#;0}), CRIT: {currentCrit:P1} → {nextCrit:P1} ({CritDiff:+0.0%;-0.0%;0.0%})";
}