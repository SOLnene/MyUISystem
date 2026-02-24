using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelSystem
{
    public int Level { get; private set; }
    public int CurrentExp { get; private set; }
    public int NextLevelExp { get; private set; }
    public int Rarity { get; private set; }

    public LevelSystem(int level, int currentExp, int rarity)
    {
        Level = level;
        CurrentExp = currentExp;
        Rarity = rarity;
        NextLevelExp = GetExpRequired();
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
    /// <summary>
    /// 获取升级所需经验
    /// </summary>
    public int GetExpRequired(int level = 0)
    {
        var Level = level == 0 ? this.Level : level;
        if (Level < 1) return 0;
        
        // 基础参数
        const float baseExp = 100f;      // 初始经验需求
        const float growth = 1.45f;      // 成长系数（可按稀有度调整）

        // 经验需求公式
        float exp = baseExp * Mathf.Pow(Level, growth);

        // 根据稀有度放大倍数
        float rarityMultiplier = 1f + (int)Rarity * 0.3f; // 稀有度越高需求越多

        return Mathf.RoundToInt(exp * rarityMultiplier);
    }

    public bool RankMaxed()
    {
        return Level >= 90;
    }
    public int GetMaxLevel()
    {
        return 90;
    }
}
