using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
/// <summary>
/// 目前仅用于武器升级界面
/// </summary>
public class EquipItemViewModel: ItemViewModel
{
    public readonly ReactiveProperty<int> currentExp = new ReactiveProperty<int>(0);
    public readonly ReactiveProperty<int> nextLevelExp  = new ReactiveProperty<int>(1000);
    public readonly ReactiveProperty<int> level = new ReactiveProperty<int>(1);
    public readonly ReactiveProperty<int> attack = new ReactiveProperty<int>();
    public readonly ReactiveProperty<float> critical = new ReactiveProperty<float>();
    /*public readonly ReactiveProperty<int> nextAttack = new ReactiveProperty<int>();
    public readonly ReactiveProperty<float> nextCritical = new ReactiveProperty<float>();*/
    public readonly ReactiveProperty<bool> needBreak = new();
    public readonly ReactiveProperty<bool> maxRanked = new();
    public readonly ReactiveProperty<int> rank = new();
    public readonly ReactiveProperty<int> nextRankMaxLevel = new();
    public readonly ReactiveProperty<int> refineLevel = new();
    /*public readonly ReactiveProperty<string> DisplayLevelText = new();
    public readonly ReactiveProperty<string> DisplayAttackText = new();
    public readonly ReactiveProperty<string> DisplayCritRateText = new();
    */
    
    
    public new EquipItem Model => base.Model as EquipItem;
    
    public EquipItemViewModel(EquipItem equipItem): base(equipItem)
    {
        SyncFromModel();
        
        //todo:可能需要将数据同步回model
    }
    
    public void SyncFromModel()
    {
        base.SyncFormModel();
        currentExp.Value = Model.CurrentExp;
        level.Value = Model.Level;
        nextLevelExp.Value = Model.NextLevelExp;
        attack.Value = (int)Model.GetAttack();
        critical.Value = Model.GetCriticalDamage();
        /*nextAttack.Value = (int)Model.GetAttack(level.Value + 1);
        nextCritical.Value = Model.GetCriticalDamage(level.Value + 1);*/
        needBreak.Value = Model.CanBreakthrough();
        rank.Value = Model.Rank;
        maxRanked.Value = Model.RankMaxed();
        nextRankMaxLevel.Value = Model.GetNextRankMaxLevel();
        refineLevel.Value = Model.RefinementLevel;
    }

    protected override string UpdateDisplayCount()
    {
        return $"Lv.{level.Value}";
    }
    
    public void AddExp(int exp)
    {
        //测试
        Model.AddExp(exp);
        SyncFromModel();
    }

    public void NextLevel()
    {
        
    }
    
    public void Breakout()
    {
        Model.TryBreakthrough();
        SyncFromModel();
    }

    public void Refine()
    {
        Model.TryRefine();
        SyncFromModel();
    }
    
    public EquipPreview GetPreviewWithExp(int addedExp)
    {
        return Model.GetPreviewWithExp(addedExp);
    }
    
    public NextAttribute GetNextAttribute()
    {
        return new NextAttribute
        {
            attack = (int)Model.GetAttack(level.Value + 1),
            critical = Model.GetCriticalDamage(level.Value + 1)
        };

    }
}

public struct NextAttribute
{
    public int attack;
    public float critical;
}