using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;

public class EnhancePanelViewModel: IDisposable
{
    public CompositeDisposable disposables = new CompositeDisposable();

    public readonly ReactiveProperty<EquipItemViewModel> weaponVM;
    public readonly EnhanceRightBottomViewModel rightBottomVM;
    public readonly ReactiveProperty<bool> showUpgradeAttribute = new(false);
    
    public readonly ReactiveProperty<int> previewExp = new();
    public readonly ReactiveProperty<int> previewCost = new();
    public readonly ReactiveProperty<EquipPreview> previewEquip = new();
    
    public EnhancePanelViewModel(ReactiveProperty<EquipItemViewModel> viewModel)
    {
        weaponVM = viewModel;
        rightBottomVM = new EnhanceRightBottomViewModel(viewModel);
        Observable.CombineLatest(weaponVM.Where(viewModel=>viewModel!=null),
            rightBottomVM.totalExp,
            (weapon, exp) => new { weapon, exp }).Subscribe(
            x =>
            {
                if (x.weapon == null)
                {
                    return;
                }
                var preview = x.weapon.Model.GetPreviewWithExp(x.exp);
                previewExp.Value = preview.maxGainExp;
                previewCost.Value = preview.costGold;
                previewEquip.SetValueAndForceNotify(preview);
                showUpgradeAttribute.Value = preview.levelUp > 0 || preview.isBreakPreview;
            }).AddTo(disposables);
    }
    
    public void RefreshPreview()
    {
        var w = weaponVM.Value;
        if (w == null) return;

        var preview = weaponVM.Value.GetPreviewWithExp(rightBottomVM.totalExp.Value);
        previewEquip.SetValueAndForceNotify(preview);
        showUpgradeAttribute.Value = preview.levelUp > 0 || preview.isBreakPreview;
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
