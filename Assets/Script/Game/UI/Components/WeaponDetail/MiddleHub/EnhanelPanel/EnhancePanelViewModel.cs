using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.UI.Components.CharacterDetail;
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
    public readonly StatItemViewModel[] statItemVMs;
    
    //转发右下角的请求打开选择面板事件
    public readonly Subject<MaterialSelectParams> requestOpenItemSelectPanel = new();
    public EnhancePanelViewModel(ReactiveProperty<EquipItemViewModel> viewModel)
    {
        weaponVM = viewModel;
        rightBottomVM = new EnhanceRightBottomViewModel(viewModel);
        statItemVMs = new[]
        {
            new StatItemViewModel(null, "基础攻击力"),
            new StatItemViewModel(null, "暴击率")
        };
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
                statItemVMs[0].SetValue(x.weapon.attack.Value, preview.nextAtk);
                statItemVMs[1].SetValue(x.weapon.critical.Value, preview.nextCrit);
            }).AddTo(disposables);
        
        rightBottomVM.requestOpenItemSelectPanel
            .Subscribe(requestOpenItemSelectPanel.OnNext)
            .AddTo(disposables);
    }
    
    public void RefreshPreview()
    {
        var w = weaponVM.Value;
        if (w == null) return;

        var preview = weaponVM.Value.GetPreviewWithExp(rightBottomVM.totalExp.Value);
        previewEquip.SetValueAndForceNotify(preview);
        showUpgradeAttribute.Value = preview.levelUp > 0 || preview.isBreakPreview;
        statItemVMs[0].SetValue(w.attack.Value, preview.nextAtk);
        statItemVMs[1].SetValue(w.critical.Value, preview.nextCrit);
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
