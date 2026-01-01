using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class WeaponDetailViewModel: IDisposable
{
    /// <summary>
    /// 当前武器数据流，用于特有界面
    /// </summary>
    public readonly ReactiveProperty<EquipItemViewModel> currentWeaponVM = new();
    
    public readonly ReactiveProperty<int> SelectedIndex = new(0);

    public CompositeDisposable disposables = new CompositeDisposable();
    
    public WeaponDetailMiddleViewModel MiddleVM;

    public readonly InfoPanelViewModel infoVm;
    public readonly EnhancePanelViewModel enhanceVM;
    public readonly RefinePanelViewModel refineVM;
    public readonly WeaponDetailBottomViewModel bottomVM;
    
    public WeaponDetailViewModel(ReactiveProperty<EquipItemViewModel> viewModel,InventoryRepository repo)
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
    
    public void SelectTab(int index)
    {
        SelectedIndex.Value = index;
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
