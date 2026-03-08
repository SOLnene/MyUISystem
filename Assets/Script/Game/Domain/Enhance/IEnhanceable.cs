using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using Game.Domain.Enhance;
using UniRx;

public interface IEnhanceable
{
    LevelSystem LevelSystem { get; }
    
    public IReadOnlyReactiveProperty<int> LevelRP { get; }
    public IReadOnlyReactiveProperty<int> ExpRP { get; }
    
    /// <summary>
    /// 用于展示的属性
    /// </summary>
    /// <param name="addedExp"></param>
    /// <returns></returns>
    public List<StatPreviewData> GetStatPreview(int addedExp);
    /*int Level { get; }
    int CurrentExp { get; }


    int GetNextLevelExp();
    bool NeedBreak();
    int GetMaxLevel();
    ExpGainResult AddExp(int exp);
    bool Breakout();*/

    //public EnhancePreview GetPreviewWithExp(int addedExp);
}

public enum ExpGainResult
{
    None,             // 未发生变化
    LeveledUp,        // 升级了
    RankLimitReached, // 达到当前Rank的等级上限
    MaxLevelReached   // 已达最大等级（完全满级）
}