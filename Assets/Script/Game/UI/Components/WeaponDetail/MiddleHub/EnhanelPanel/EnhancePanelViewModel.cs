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
    
    public readonly EnhanceLevelPreviewViewModel enhanceLevelPreviewVm;
    public readonly PromoteLevelPreviewViewModel promotePreviewVm;
    public readonly PromoteMaterialPreviewViewModel promoteMaterialPreviewVm = new();
    
    //转发右下角的请求打开选择面板事件
    public readonly Subject<MaterialSelectParams> requestOpenItemSelectPanel = new();
    public EnhancePanelViewModel(ReactiveProperty<EquipItemViewModel> viewModel)
    {
        weaponVM = viewModel;
        rightBottomVM = new EnhanceRightBottomViewModel(viewModel);
        enhanceLevelPreviewVm = weaponVM.Value != null
            ? new EnhanceLevelPreviewViewModel(weaponVM.Value.Model,previewExp)
            : null;
        
        promotePreviewVm = weaponVM.Value != null
            ? new PromoteLevelPreviewViewModel(weaponVM.Value.Model)
            : null;
        
        statItemVMs = new[]
        {
            new StatItemViewModel(null, "基础攻击力"),
            new StatItemViewModel(null, "暴击率")
        };
        
        //weaponvm发出变化信号时，返回weaponvn并且只监听最新的武器vm
        var weaponChanged = weaponVM
            .Where(weapon => weapon != null)
            .Select(weapon => weapon.Changed.StartWith(Unit.Default).Select(_ => weapon))
            .Switch();
        Observable.CombineLatest(
                weaponChanged,
                rightBottomVM.totalExp,
                (weapon, exp) => new { weapon, exp })
            .Subscribe(x =>
            {
                UpdatePreview(x.weapon,x.exp);
            })
            .AddTo(disposables);
        
        rightBottomVM.requestOpenItemSelectPanel
            .Subscribe(requestOpenItemSelectPanel.OnNext)
            .AddTo(disposables);
    }
    
    void UpdatePreview(EquipItemViewModel weapon, int exp)
    {
        if (weapon == null) return;

        var preview = weapon.Model.GetPreviewWithExp(exp);

        previewExp.Value = preview.maxGainExp;
        previewCost.Value = preview.costGold;
        previewEquip.SetValueAndForceNotify(preview);
        showUpgradeAttribute.Value = preview.levelUp > 0 || preview.isBreakPreview;
        statItemVMs[0].SetValue(weapon.attack.Value, preview.nextAtk);
        statItemVMs[1].SetValue(weapon.critical.Value, preview.nextCrit);
    }

    void RefreshPreviewCost(int enhanceCost)
    {
        var weapon = weaponVM.Value;
        if (weapon == null)
        {
            previewCost.Value = 0;
            return;
        }

        previewCost.Value = weapon.needBreak.Value
            ? weapon.Model.GetPromoteGoldCost()
            : enhanceCost;
    }
    
    public void ClearSelectedMaterials()
    {
        rightBottomVM.ClearSelectedMaterials();
    }

    public void RefreshPromoteMaterialPreview()
    {
        var weapon = weaponVM.Value;
        if (weapon == null)
        {
            promoteMaterialPreviewVm.SetMaterials(null);
            return;
        }

        var promoteDefinition = GameDatabase.PromoteDatabase.Get("hutao");
        if (promoteDefinition == null || weapon.Model.Rank >= promoteDefinition.rankRules.Count)
        {
            promoteMaterialPreviewVm.SetMaterials(null);
            return;
        }

        promoteMaterialPreviewVm.SetMaterials(promoteDefinition.rankRules[weapon.Model.Rank].materials);
    }
    
    public void Dispose()
    {
        enhanceLevelPreviewVm?.Dispose();
        promotePreviewVm?.Dispose();
        disposables.Dispose();
    }
}
