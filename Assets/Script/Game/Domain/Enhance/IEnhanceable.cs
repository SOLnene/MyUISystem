using System;
using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using Game.Domain.Enhance;
using UniRx;

public interface IEnhanceable
{
    //TODO:避免外部引用levelsystem,在model中封装方法
    LevelSystem LevelSystem { get; }
    
    public IReadOnlyReactiveProperty<int> LevelRP { get; }
    public IReadOnlyReactiveProperty<int> ExpRP { get; }
    
    IObservable<Unit> ChangeRP { get; }
    /// <summary>
    /// 用于展示的属性
    /// </summary>
    /// <param name="addedExp"></param>
    /// <returns></returns>
    public List<StatPreviewData> GetStatPreview(int addedExp,bool promoting = false);
    
    /// <summary>
    /// 实际增加经验，驱动等级与经验的 ReactiveProperty 更新
    /// </summary>
    /// <param name="exp">增加的经验值</param>
    /// <returns>本次经验变化的结果</returns>
    ExpGainResult AddExp(int exp);
}

public enum ExpGainResult
{
    None,             // 未发生变化
    LeveledUp,        // 升级了
    RankLimitReached, // 达到当前Rank的等级上限
    MaxLevelReached   // 已达最大等级（完全满级）
}