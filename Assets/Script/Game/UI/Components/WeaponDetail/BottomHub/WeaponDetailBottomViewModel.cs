using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class WeaponDetailBottomViewModel : IDisposable
{
    public readonly ReactiveProperty<int> totalCostGold = new();

    /// <summary> 按钮交互命令 </summary>
    public readonly ReactiveCommand onStoryClick = new();
    public readonly ReactiveCommand onQuickEquipClick = new();
    public readonly ReactiveCommand onEnhanceClick = new();
    public readonly ReactiveCommand onBreakoutClick = new();
    
    public readonly ReactiveProperty<bool> canBreakout = new(false);
    
    public readonly ReactiveProperty<int> selectedTabIndex = new(0);
    
    
    
    CompositeDisposable disposables = new();

    public WeaponDetailBottomViewModel()
    {
        // 默认行为，可替换
        onStoryClick.Subscribe(_ => Debug.Log("查看武器故事")).AddTo(disposables);
        onQuickEquipClick.Subscribe(_ => Debug.Log("快速装备执行")).AddTo(disposables);
        onEnhanceClick.Subscribe(_ =>
                Debug.Log($"执行强化:金币-{totalCostGold.Value}")
            ).AddTo(disposables);
        onBreakoutClick.Subscribe(_ => Debug.Log("执行突破")).AddTo(disposables);
    }

    public void SetIndex(int index)
    {
        selectedTabIndex.Value = index;
    }
    
    public void SetValues(int gold)
    {
        totalCostGold.Value = gold;
    }
    
    public void Dispose()
    {
        disposables.Dispose();
    }
}
