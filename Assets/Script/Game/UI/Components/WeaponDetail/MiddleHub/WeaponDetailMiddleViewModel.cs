using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public enum WeaponDetailTab
{
    Info = 0 , //详情
    Enhance = 1, //强化/突破
    Refine = 2, //精炼
}

public class WeaponDetailMiddleViewModel : IDisposable
{
    public readonly ReactiveProperty<EquipItemViewModel> currentWeaponVM = new();
    
    public readonly ReactiveProperty<int> currentTabIndex;
    
    readonly CompositeDisposable disposable = new CompositeDisposable();

    public WeaponDetailMiddleViewModel(ReactiveProperty<EquipItemViewModel> viewModel, ReactiveProperty<int> currentTabIndex)
    {
        disposable.Clear();
        currentWeaponVM = viewModel;
        this.currentTabIndex = currentTabIndex;
    }

    public void SetWeapon(EquipItemViewModel viewModel)
    {
        currentWeaponVM.Value = viewModel;
    }

    public void SelectTab(int index)
    {
        currentTabIndex.Value = index;
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
