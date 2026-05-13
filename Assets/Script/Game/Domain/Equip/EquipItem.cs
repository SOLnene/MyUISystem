using System;
using System.Collections;
using System.Collections.Generic;
using Game.Domain.Enhance;
using UniRx;
using UnityEngine;

[Serializable]
public class EquipItem : InventoryItem, IEnhanceable, IPromotable
{
    public int Level => LevelSystem.Level;
    public int RefinementLevel { get; private set; }
    
    public int CurrentExp => LevelSystem.CurrentExp;
    //todo:改成计算，删了这个
    public int NextLevelExp => LevelSystem.NextLevelExp; 
    
    public int GetNextLevelExp() => LevelSystem.NextLevelExp;
    public int Rank => RankSystem.CurrentRank;
    readonly ReactiveProperty<int> levelRP;
    readonly ReactiveProperty<int> expRP;
    readonly ReactiveProperty<int> rankRP;
    
    public LevelSystem LevelSystem { get; private set; }
    public RankSystem RankSystem { get; private set; }
    public EquipStats Stats { get; }
    public IReadOnlyReactiveProperty<int> LevelRP => levelRP;
    public IReadOnlyReactiveProperty<int> ExpRP => expRP;
    public IReadOnlyReactiveProperty<int> RankRP => rankRP;
    public IObservable<Unit> ChangeRP { get; }
    
    public new EquipDefinition EquipDefinition => base.ItemDefinition as EquipDefinition;
    
    public EquipItem(EquipDefinition def, int level = 1, int refine = 1,int currentExp = 0 , int nextLevelExp = 1000) : base(def)
    {
        RefinementLevel = refine;
        LevelSystem = new LevelSystem(level, currentExp, (int)ItemRarity);
        RankSystem = new RankSystem();
        Stats = new EquipStats();
        levelRP = new ReactiveProperty<int>(Level);
        expRP = new ReactiveProperty<int>(CurrentExp);
        rankRP = new ReactiveProperty<int>(Rank);
        ChangeRP = Observable.CombineLatest(LevelRP, RankRP)
            .Select(_ => Unit.Default);
        RefreshBaseStats();
    }


    public override string GetDisplayLevelText() => $"Lv.{Level}\n";

    /// <summary>
    /// 详情面板中的主要属性显示(第二部分)
    /// 现在都当成爆伤，没有别的加成属性
    /// todo:model层不该管怎么显示
    /// </summary>
    /// <returns></returns>
    public override string GetDisplayMainText() => $"暴击伤害\n<b>{GetCriticalDamage()}%</b>\n基础攻击力\n<b><size=150%>{GetAttack()}</size></b>";

    public string GetDisplayMainStatText() => $"{GetAttack()}";
    
    public string GetDisplaySubStatText() => $"{GetCriticalDamage()}";
    
    public string GetDisplayExpText() => $"{CurrentExp}/{NextLevelExp}%";
    void RefreshBaseStats()
    {
        Stats.BaseAttack.Value = GetAttack(Level);
        Stats.CriticalDamage.Value = GetCriticalDamage(Level);
    }
    
    public int GetAttack(int level = 0, int rank = -1)
    {
        if (level == 0 && rank < 0 && Stats != null)
            return Mathf.RoundToInt(Stats.BaseAttack.Value);
        
        var lv = level == 0 ? Level : level;
        var currentRank = rank < 0 ? RankSystem.CurrentRank : rank;
        // 基础攻击由武器定义决定
        int baseAttack = EquipDefinition.baseAttack; // e.g. 100~200
        // 等级成长，简单线性或略微非线性
        float levelMultiplier = 1 + 0.05f * (lv - 1); // 每级增加5%
        // 精炼加成，按星级/精炼等级加固定百分比
        float refineMultiplier = 1 + 0.02f * RefinementLevel; // 每级精炼增加2%
    
        return Mathf.RoundToInt(baseAttack * levelMultiplier * refineMultiplier * (1 + 0.08f * currentRank));
    }

    public float GetCriticalDamage(int level = 0, int rank = -1)
    {
        if (level == 0 && rank < 0 && Stats != null)
            return Stats.CriticalDamage.Value;
        
        var lv = level == 0 ? Level : level;
        var currentRank = rank < 0 ? RankSystem.CurrentRank : rank;
        // 基础暴伤：0.5 = 50%
        float baseCritDamage = EquipDefinition.baseCritDamage;

        // 精炼加成，每级精炼增加 5%
        float refineBonus = 0.05f * RefinementLevel;

        // 星级加成，每颗星增加 10%
        float starBonus = 0.1f * Stars;

        return baseCritDamage + refineBonus + starBonus + 0.02f * currentRank;
    }

    /// <summary>
    /// 获取升级到目标等级所需经验
    /// </summary>
    public int GetExpRequired(int level = 0)
    {
        return GetExpRequiredForLevel(level == 0 ? Level : level);
    }

    public int GetExpRequiredForLevel(int targetLevel)
    {
        if (targetLevel <= 1) return 0;
        
        // 基础参数
        const float baseExp = 100f;      // 初始经验需求
        const float growth = 1.45f;      // 成长系数（可按稀有度调整）

        // 经验需求公式
        float exp = baseExp * Mathf.Pow(targetLevel, growth);

        // 根据稀有度放大倍数
        float rarityMultiplier = 1f + (int)ItemRarity * 0.3f; // 稀有度越高需求越多

        return Mathf.RoundToInt(exp * rarityMultiplier);
    }
    
    public int GetEnhanceCost(int gainedExp)
    {
        return GrowthCostFormula.GetEnhanceGoldCost(gainedExp);
    }
    
    /// <summary>
    /// 获取该装备作为素材时能提供的经验值
    /// 稀有度越高，提供经验越多
    /// </summary>
    public int GetExpValue()
    {
        // baseExp = 50，稀有度和等级加成
        int baseExp = GetBaseWeaponMaterialExp(Stars);
        int recycledExp = Mathf.RoundToInt(GetInvestedExp() * 0.8f);
        return baseExp + recycledExp;
    }

    int GetInvestedExp()
    {
        int total = CurrentExp;
        for (int level = 1; level < Level; level++)
        {
            total += LevelSystem.GetExpRequired(level);
        }

        return total;
    }

    static int GetBaseWeaponMaterialExp(int stars)
    {
        if (stars <= 1) return 600;
        if (stars == 2) return 1200;
        if (stars == 3) return 1800;
        if (stars == 4) return 50000;
        return 300000;
    }

    // 获取当前 rank 的最大等级
    public int GetCurrentMaxLevel()
    {
        return RankSystem.CurrentRankMaxLevel;
    }

    // 是否可以突破（按当前等级和当前 rank）
    public bool NeedBreak()
    {
        return RankSystem.CanPromote(Level);
    }

    /// <summary>
    /// 是否已经突破满了
    /// </summary>
    /// <returns></returns>
    public bool RankMaxed()
    {
        return RankSystem.IsMaxRank();
    }
    
    public int GetNextRankMaxLevel()
    {
        // 如果已经是最高Rank，则返回当前Rank的最大等级
        if (RankMaxed())
            return GetCurrentMaxLevel();

        return RankSystem.GetNextRankMaxLevel();
    }
    
    // 尝试突破：检查材料，通过 inventoryService 扣除材料，提升 Rank（返回是否成功）
    public bool Breakout()
    {
        return Promote();
    }
    
    public bool Promote()
    {
        if (!NeedBreak()) return false;

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
        if (!RankSystem.Promote())
            return false;
        
        rankRP.Value = Rank;
        RefreshBaseStats();
        //OnRankChanged?.Invoke(Rank);

        // 注意：突破后通常会把 level cap 提升，但不自动满级；保持当前 Level 不变
        return true;
    }
    
    public int GetPromoteGoldCost()
    {
        return GrowthCostFormula.GetPromoteGoldCost(RankSystem.CurrentRank, (int)ItemRarity + 1);
    }

    public bool TryRefine()
    {
        if(RefinementLevel>=GetRefineCap()) return false;
        RefinementLevel++;
        RefreshBaseStats();
        return true;
    }

    
    public int GetRefineCost(int currentLevel)
    {
        int star = Mathf.Max(1, Stars);
        int baseCost = 500 * star * star;
        float exponent = 1.15f;
        return Mathf.RoundToInt(baseCost * Mathf.Pow(currentLevel, exponent));
    }
    
    public int GetRefineCap()
    {
        //默认为5
        return 5;
    }
    
    public EquipPreview GetPreviewWithExp(int addedExp, bool promoting = false)
    {
        int maxLevel = GetCurrentMaxLevel();
        var levelPreview = LevelSystem.GetPreviewWithExp(addedExp, maxLevel);
        int previewRank = promoting && !RankSystem.IsMaxRank()
            ? RankSystem.CurrentRank + 1
            : RankSystem.CurrentRank;
        int previewLevel = promoting ? Level : levelPreview.finalLevel;
       
        // 生成预览结构
        var preview = new EquipPreview
        {
            currentAtk = GetAttack(),
            nextAtk = GetAttack(previewLevel, previewRank),
            currentCrit = GetCriticalDamage(),
            nextCrit = GetCriticalDamage(previewLevel, previewRank),
            isBreakPreview = promoting || levelPreview.finalLevel >= maxLevel && NeedBreak(),
            levelUp = promoting ? 0 : levelPreview.levelUpCount,
            maxGainExp = promoting ? 0 : levelPreview.cappedExpGain,
            costGold = promoting ? GetPromoteGoldCost() : GetEnhanceCost(levelPreview.cappedExpGain)
        };

        return preview;
    }

    public List<StatPreviewData> GetStatPreview(int addedExp,bool promoting = false)
    {
        var preview = GetPreviewWithExp(addedExp, promoting);
        return new List<StatPreviewData>
        {
            new StatPreviewData
            {
                label = "基础攻击力",
                currentValue = preview.currentAtk,
                nextValue = preview.nextAtk
            },
            new StatPreviewData
            {
                label = "暴击伤害",
                currentValue = preview.currentCrit,
                nextValue = preview.nextCrit
            }
        };
    }
    
    public ExpGainResult AddExp(int exp)
    {
        var result = LevelSystem.AddExp(exp, GetCurrentMaxLevel());
        RefreshBaseStats();
        levelRP.Value = Level;
        expRP.Value = CurrentExp;
        return result;
    }
    
    
    void LevelUp()
    {
        LevelSystem.AddExp(NextLevelExp, GetCurrentMaxLevel());
        RefreshBaseStats();
        levelRP.Value = Level;
        expRP.Value = CurrentExp;
    }
}

// 单阶信息（类似“突破1：上限40，需要材料...，额外属性...”）
//todo:删了
[Serializable]
public class RankInfo
{
    public int rank;             // 0 = 未突破（基础），1 = 突破一次 ...
    public int maxLevel;         // 该 rank 的等级上限 (例如 20,40,60...)
    public int goldCost;   
    public List<PromoteMaterialCost> requirements = new();
    // 可扩展：突破带来的固定属性加成或解锁（例如攻击力提升、精炼上限等）
    public float attackAddFlat = 0f;      // 额外固定攻击
    public float attackAddPercent = 0f;   // 额外百分比（如 +5%）
}

//todo:升级只管等级不管属性
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
