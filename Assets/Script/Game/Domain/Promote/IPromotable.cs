using System;
using System.Collections.Generic;
using UniRx;

/// <summary>
/// 抽象“可突破 / 有阶级”的实体（角色、武器等）需要实现的最小接口。
/// 先只写当前角色强化面板会用到的成员，后续可以按需扩展。
/// </summary>
public interface IPromotable : ILevelCapped
{
    IReadOnlyReactiveProperty<int> RankRP { get; }
    RankSystem RankSystem { get; }
    
    /// <summary>
    /// 用于展示的属性
    /// </summary>
    /// <param name="addedExp"></param>
    /// <returns></returns>
    public List<StatPreviewData> GetStatPreview(int addedExp,bool promoting = false);

    IObservable<Unit> ChangeRP { get; }
    bool Promote();

    int GetPromoteGoldCost();
}

