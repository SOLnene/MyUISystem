using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public enum WeaponDetailTab
{
    Info = 0 , //详情
    Enhance = 1, //强化
    Refine = 2, //精炼
}

public class WeaponDetailMiddleViewModel : IDisposable
{
    public readonly ReactiveProperty<EquipItemViewModel> currentWeaponVM = new();
    
    public readonly ReactiveProperty<int> currentTabIndex = new ReactiveProperty<int>(0);
    
    readonly CompositeDisposable disposables = new CompositeDisposable();

    public WeaponDetailMiddleViewModel(ReactiveProperty<EquipItemViewModel> viewModel)
    {
        currentWeaponVM = viewModel;
    }

    public void SetWeapon(EquipItemViewModel viewModel)
    {
        currentWeaponVM.Value = viewModel;
    }

    
    public void Dispose()
    {
        disposables.Dispose();
    }
}
