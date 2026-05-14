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
    
    internal void ResetCurrentExp()
    {
        CurrentExp = 0;
        NextLevelExp = GetExpRequired();
    }
    
    public ExpGainResult AddExp(int exp,int maxLevel)
    {
        if (RankMaxed())
            return ExpGainResult.MaxLevelReached;
        
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
        Debug.Log("Added Exp: " + exp + ", Current Exp: " + CurrentExp + ", Level: " + Level);
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

    public LevelPreview GetPreviewWithExp(int addedExp,int maxLevel)
    {
        int tempLevel = Level;
        int tempExp = CurrentExp + addedExp;
        int levelUpCount = 0;
        int cappedExpGain = addedExp;

        while (tempExp >= GetExpRequired(tempLevel))
        {
            if (tempLevel >= maxLevel)
            {
                // 经验超出但受Rank限制
                cappedExpGain -= tempExp - (GetExpRequired(tempLevel) - 1);
                tempExp = GetExpRequired(tempLevel) - 1;
                break;
            }

            tempExp -= GetExpRequired(tempLevel);
            tempLevel++;
            levelUpCount++;
        }

        return new LevelPreview
        {
            finalLevel = tempLevel,
            finalExp = tempExp,
            levelUpCount = levelUpCount,
            cappedExpGain = cappedExpGain
         };
    }
    
    public bool RankMaxed()
    {
        return Level >= 90;
    }
}

public struct LevelPreview
{
    public int finalLevel;
    public int finalExp;
    public int levelUpCount;
    public int cappedExpGain;
}
