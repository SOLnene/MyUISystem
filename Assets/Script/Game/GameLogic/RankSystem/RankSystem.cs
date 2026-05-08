using System;


public class RankSystem
{
    // 原神式阶级与等级上限，固定写死
    // 0阶:20级，1阶:40级，2阶:50级，3阶:60级，4阶:70级，5阶:80级，6阶:90级
    private static readonly int[] RankMaxLevels = { 20, 40, 50, 60, 70, 80, 90 };

    /// <summary>
    /// 当前阶级（0起始）
    /// </summary>
    public int CurrentRank { get; private set; }

    /// <summary>
    /// 最大阶级
    /// </summary>
    public int MaxRank => RankMaxLevels.Length - 1;

    /// <summary>
    /// 当前阶级等级上限
    /// </summary>
    public int CurrentRankMaxLevel => RankMaxLevels[CurrentRank];

    public RankSystem(int initialRank = 0)
    {
        CurrentRank = initialRank;
    }

    /// <summary>
    /// 当前等级是否已到该阶级等级上限
    /// </summary>
    public bool IsAtRankMaxLevel(int currentLevel)
    {
        return currentLevel >= CurrentRankMaxLevel;
    }

    /// <summary>
    /// 能否突破到下一阶（只需满级，无需材料，存在下一阶即可）
    /// </summary>
    public bool CanPromote(int currentLevel)
    {
        if (CurrentRank >= MaxRank)
            return false;
        return currentLevel >= CurrentRankMaxLevel;
    }

    public bool IsMaxRank()
    {
        return CurrentRank >= MaxRank;
    }

    /// <summary>
    /// 执行突破操作，无需材料。只能在CanPromote为true时调用。
    /// </summary>
    public bool Promote()
    {
        if (CurrentRank >= MaxRank)
            return false;
        CurrentRank++;
        return true;
    }

    /// <summary>
    /// 获取当前阶级的等级上限。如果已为最大阶则返回null
    /// </summary>
    public int GetCurrentRankMaxLevel()
    {
        if (CurrentRank >= MaxRank)
            return 90;
        return RankMaxLevels[CurrentRank];
    }
    
    /// <summary>
    /// 获取下一个阶级的等级上限。如果已为最大阶则返回null
    /// </summary>
    public int GetNextRankMaxLevel()
    {
        if (CurrentRank >= MaxRank)
            return 90;
        return RankMaxLevels[CurrentRank + 1];
    }
    
}
