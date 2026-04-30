using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class EquipDetailViewModel: IDisposable
{
    /// <summary>
    /// 当前武器数据流，用于特有界面
    /// </summary>
    public readonly ReactiveProperty<EquipItemViewModel> currentWeaponVM = new();
    
    //todo:这个或者middle里面的index作为唯一状态源
    //public readonly ReactiveProperty<int> SelectedIndex = new(0);

    public CompositeDisposable disposables = new CompositeDisposable();
    
    public WeaponDetailMiddleViewModel MiddleVM;

    public readonly InfoPanelViewModel infoVm;
    public readonly EnhancePanelViewModel enhanceVM;
    public readonly RefinePanelViewModel refineVM;
    public readonly WeaponDetailBottomViewModel bottomVM;
    
    public EquipDetailViewModel(ReactiveProperty<EquipItemViewModel> viewModel,InventoryRepository repo)
    {
        currentWeaponVM = viewModel;
        
        MiddleVM = new WeaponDetailMiddleViewModel(currentWeaponVM);
        infoVm = new InfoPanelViewModel();
        enhanceVM = new EnhancePanelViewModel(currentWeaponVM);
        refineVM = new RefinePanelViewModel(currentWeaponVM);
        bottomVM = new WeaponDetailBottomViewModel();
        /*
        currentItem = currentWeaponVM
            .Select(w => (InventoryItem)w)
            .ToReadOnlyReactiveProperty();
            */
        
        currentWeaponVM.Subscribe(weapon =>
        {
            MiddleVM.SetWeapon(weapon);
        }).AddTo(disposables);

        currentWeaponVM
            .Where(viewModel => viewModel != null)
            .Subscribe(viewModel => infoVm.Bind(viewModel))
            .AddTo(disposables);
        MiddleVM.currentTabIndex.Subscribe(index => bottomVM.SetIndex(index)).AddTo(disposables);

        enhanceVM.previewCost.Subscribe(cost =>
        {
            bottomVM.totalCostGold.Value = cost;
        }).AddTo(disposables);
        
        refineVM.previewCost.Subscribe(cost =>
        {
            bottomVM.totalCostGold.Value = cost;
        }).AddTo(disposables);
        
        bottomVM.onEnhanceClick.Subscribe(_ =>
        {
            if (GameEconomy.Instance.TrySpendGold(enhanceVM.previewCost.Value)||true)
            {
                currentWeaponVM.Value.AddExp(enhanceVM.previewExp.Value);
                enhanceVM.RefreshPreview();
            }
            else
            {
                Debug.Log("金币不足，无法强化");
            }
        }).AddTo(disposables);

        currentWeaponVM.Value.needBreak.Subscribe(need =>
        {
            bottomVM.canBreakout.Value = need;
        }).AddTo(disposables);
        
        bottomVM.onBreakoutClick.Subscribe(_ =>
        {
            currentWeaponVM.Value.Breakout();
        }).AddTo(disposables);
    }
    
    public void SetWeapon(EquipItemViewModel viewModel)
    {
        currentWeaponVM.Value = viewModel;
    }
    
    public void ApplyOpenParams(EquipDetailOpenParams param)
    {
        SetWeapon(param.Weapon);
        SelectTab((int)param.InitialTab);
    }
    
    public void SelectTab(int index)
    {
        //SelectedIndex.Value = index;
        MiddleVM.currentTabIndex.Value = index;
    }
    
    public void Dispose()
    {
        disposables.Dispose();
        MiddleVM.Dispose();
        infoVm.Dispose();
        enhanceVM.Dispose();
        bottomVM.Dispose();
    }
}

/// <summary>
/// 武器界面跳转参数
/// </summary>
public class EquipDetailOpenParams
{
    public EquipItemViewModel Weapon { get; }
    public WeaponDetailTab InitialTab { get; }

    public EquipDetailOpenParams(EquipItemViewModel weapon, WeaponDetailTab initialTab)
    {
        Weapon = weapon;
        InitialTab = initialTab;
    }
}
