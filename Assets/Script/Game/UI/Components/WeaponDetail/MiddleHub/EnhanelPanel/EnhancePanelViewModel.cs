using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Enhance;
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
    internal readonly ReactiveProperty<EnhancementPreviewData> enhancementPreview = new();
    
    //转发右下角的请求打开选择面板事件
    public readonly Subject<MaterialSelectParams> requestOpenItemSelectPanel = new();
    public EnhancePanelViewModel(ReactiveProperty<EquipItemViewModel> viewModel)
    {
        weaponVM = viewModel;
        rightBottomVM = new EnhanceRightBottomViewModel(viewModel);
        enhanceLevelPreviewVm = weaponVM.Value != null
            ? new EnhanceLevelPreviewViewModel(enhancementPreview.Where(preview => preview != null))
            : null;
        
        promotePreviewVm = weaponVM.Value != null
            ? new PromoteLevelPreviewViewModel(weaponVM.Value.Model)
            : null;
        
        statItemVMs = new[]
        {
            new StatItemViewModel(null, "基础攻击力"),
            new StatItemViewModel(null, "暴击伤害", StatValueFormat.Percent),
            new StatItemViewModel(null, "基础攻击力"),
            new StatItemViewModel(null, "暴击伤害", StatValueFormat.Percent)
        };
        
        //weaponvm发出变化信号时，返回weaponvn并且只监听最新的武器vm
        var weaponChanged = weaponVM
            .Where(weapon => weapon != null)
            // EXP may change without a level change or a ViewModel synchronization.
            .Select(weapon => weapon.Changed.Merge(weapon.Model.ExpRP.AsUnitObservable())
                .StartWith(Unit.Default).Select(_ => weapon))
            .Switch();
        Observable.CombineLatest(
                weaponChanged,
                rightBottomVM.providedExp,
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
        var data = EnhancementPreviewCalculator.ForWeapon(weapon.Model, exp, out var preview);
        enhancementPreview.Value = data;
        rightBottomVM.ApplyPreview(data);

        if (weapon.needBreak.Value)
        {
            UpdatePromotePreview(weapon);
            return;
        }

        UpdateEnhancePreview(data, preview);
    }

    void UpdateEnhancePreview(EnhancementPreviewData data, EquipPreview preview)
    {
        var statPreviews = data.Stats;

        previewExp.Value = preview.maxGainExp;
        previewCost.Value = preview.costGold;
        previewEquip.SetValueAndForceNotify(preview);
        showUpgradeAttribute.Value = preview.levelUp > 0 || preview.isBreakPreview;
        
        if (statPreviews.Count > 0)
            statItemVMs[0].SetValue(statPreviews[0].currentValue, statPreviews[0].nextValue);
        if (statPreviews.Count > 1)
            statItemVMs[1].SetValue(statPreviews[1].currentValue, statPreviews[1].nextValue);
    }

    void UpdatePromotePreview(EquipItemViewModel weapon)
    {
        if (CloudGameSaveSession.Current?.PendingPromote?.target.Identity == "weapon:" + weapon.Model.InstanceId) return;
        var statPreviews = weapon.Model.GetStatPreview(0, true);

        previewExp.Value = 0;
        previewCost.Value = weapon.Model.GetPromoteGoldCost();
        showUpgradeAttribute.Value = true;
        
        if (statPreviews.Count > 0)
            statItemVMs[2].SetValue(statPreviews[0].currentValue, statPreviews[0].nextValue);
        if (statPreviews.Count > 1)
            statItemVMs[3].SetValue(statPreviews[1].currentValue, statPreviews[1].nextValue);
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

        foreach (var slot in promoteMaterialPreviewVm.itemSlotViewModels) slot.Dispose();
        promoteMaterialPreviewVm.itemSlotViewModels.Clear();
        promoteMaterialPreviewVm.itemSlotViewModels.AddRange(PromotionActionViewModel.CreateMaterialSlots(weapon.Model));
    }
    
    public void Dispose()
    {
        enhanceLevelPreviewVm?.Dispose();
        promotePreviewVm?.Dispose();
        disposables.Dispose();
        enhancementPreview.Dispose();
    }
}
