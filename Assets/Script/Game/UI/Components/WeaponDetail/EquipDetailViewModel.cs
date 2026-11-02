using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public partial class EquipDetailViewModel: IDisposable
{
    /// <summary>
    /// 当前武器数据流，用于特有界面
    /// </summary>
    public readonly ReactiveProperty<EquipItemViewModel> currentWeaponVM = new();
    public readonly ReactiveProperty<int> currentTabIndex = new((int)WeaponDetailTab.Info);
    
    //todo:这个或者middle里面的index作为唯一状态源
    //public readonly ReactiveProperty<int> SelectedIndex = new(0);

    public CompositeDisposable disposables = new CompositeDisposable();
    
    public WeaponDetailMiddleViewModel MiddleVM;

    public readonly InfoPanelViewModel infoVm;
    public readonly EnhancePanelViewModel enhanceVM;
    public readonly RefinePanelViewModel refineVM;
    public readonly WeaponDetailBottomViewModel bottomVM;
    
    /// 转发右下角的请求打开选择面板事件
    public readonly Subject<MaterialSelectParams> requestOpenItemSelectPanel = new();
    /// 转发右下角的请求关闭选择面板事件
    public readonly Subject<Unit> requestCloseItemSelectPanel = new();
    public readonly Subject<Unit> requestRefreshContentWithAnimation = new();
    public readonly Subject<EnhanceResultData> requestPlayEnhanceResult = new();
    public readonly Subject<PromoteLevelResultData> requestPlayPromoteResult = new();
    public readonly Subject<RefineResultData> requestPlayRefineResult = new();
    public EquipDetailViewModel(ReactiveProperty<EquipItemViewModel> viewModel,InventoryRepository repo)
    {
        currentWeaponVM = viewModel;
        
        MiddleVM = new WeaponDetailMiddleViewModel(currentWeaponVM, currentTabIndex);
        infoVm = new InfoPanelViewModel();
        enhanceVM = new EnhancePanelViewModel(currentWeaponVM);
        refineVM = new RefinePanelViewModel(currentWeaponVM);
        bottomVM = new WeaponDetailBottomViewModel(currentTabIndex);
        /*
        currentItem = currentWeaponVM
            .Select(w => (InventoryItem)w)
            .ToReadOnlyReactiveProperty();
            */
        
        currentWeaponVM
            .Where(viewModel => viewModel != null)
            .Subscribe(viewModel =>
            {
                infoVm.Bind(viewModel);
                bottomVM.canBreakout.Value = viewModel.needBreak.Value;
                bottomVM.isRefineMaxed.Value = viewModel.IsRefineMaxed();
            })
            .AddTo(disposables);
        enhanceVM.previewCost.Subscribe(cost =>
        {
            bottomVM.totalCostGold.Value = cost;
        }).AddTo(disposables);
        enhanceVM.previewExp.Subscribe(exp =>
        {
            UpdateEnhanceAvailability();
        }).AddTo(disposables);
        
        refineVM.previewCost.Subscribe(cost =>
        {
            bottomVM.totalCostGold.Value = cost;
        }).AddTo(disposables);
        
        bottomVM.onEnhanceClick.Subscribe(_ =>
        {
            if (!bottomVM.canEnhance.Value || refineBusy.Value) return;
            if (IsCloudEnhancement) { SubmitCloudEnhancementAsync().Forget(); return; }
            
            if (enhanceVM.previewExp.Value <= 0 || currentWeaponVM.Value.Model.Level >= currentWeaponVM.Value.Model.GetCurrentMaxLevel())
            {
                return;
            }
            var materials = enhanceVM.rightBottomVM.SelectedMaterialIds
                .Select(id => repo.TryGetEquip(id, out var item) ? item : null).ToArray();
            if (materials.Length == 0 || materials.Any(m => m == null || !GameSaveCoordinator.CanConsumeForEnhancement(m, currentWeaponVM.Value.Model))) return;
            if (GameEconomy.Instance.TrySpendGold(enhanceVM.previewCost.Value))
            {
                var weapon = currentWeaponVM.Value;
                int oldLevel = weapon.level.Value;
                float oldProgress = GetExpProgress(weapon);
                int levelUpCount = enhanceVM.enhanceLevelPreviewVm.levelUpCount.Value;
                var result = enhanceVM.enhancementPreview.Value.Level;
                weapon.Model.ApplyEnhancementState(result.finalLevel, result.finalExp);
                weapon.SyncFromModel();
                foreach (var material in materials) repo.RemoveItem(material);
                int newLevel = weapon.level.Value;
                float newProgress = GetExpProgress(weapon);
                bool needSwitchContent = weapon.needBreak.Value;
                Color rarityColor = RarityConfig.GetColor(weapon.Model.ItemRarity);
                requestPlayEnhanceResult.OnNext(new EnhanceResultData(oldLevel, newLevel, oldProgress, newProgress, levelUpCount, needSwitchContent, rarityColor));
                AchievementProgressService.Instance.AddProgress(
                    AchievementProgressKeys.WeaponEnhance);
                enhanceVM.ClearSelectedMaterials();
                requestCloseItemSelectPanel.OnNext(Unit.Default);
                GameSaveCoordinator.Instance.MarkDirty();
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
            if (IsCloudEnhancement) { promotion.SubmitAsync().Forget(); return; }
            if (refineBusy.Value || (CloudGameSaveSession.Current?.HasPendingRefine ?? false)) return;
            if (enhanceBusy.Value || (CloudGameSaveSession.Current?.HasPendingEnhance ?? false)) return;
            var weapon = currentWeaponVM.Value;
            int oldRank = weapon.rank.Value;
            int oldMaxLevel = weapon.Model.GetCurrentMaxLevel();
            int currentLevel = weapon.level.Value;

            if (!ClientProgressionService.TryPromote(PromotionParticipant.For(weapon.Model))) return;
            weapon.SyncFromModel();

            int newRank = weapon.rank.Value;
            if (newRank == oldRank)
                return;

            int newMaxLevel = weapon.Model.GetCurrentMaxLevel();
            Color rarityColor = RarityConfig.GetColor(weapon.Model.ItemRarity);
            requestPlayPromoteResult.OnNext(new PromoteLevelResultData(oldRank, newRank, currentLevel, oldMaxLevel, newMaxLevel, rarityColor));
            GameSaveCoordinator.Instance.MarkDirty();
        }).AddTo(disposables);

        bottomVM.onRefineClick.Subscribe(_ =>
        {
            if (refineBusy.Value) return;
            if (enhanceBusy.Value || (CloudGameSaveSession.Current?.HasPendingEnhance ?? false)) return;
            if (IsCloudEnhancement) { SubmitCloudRefinementAsync().Forget(); return; }
            if (!refineVM.CanApplyRefine())
                return;

            if (GameEconomy.Instance.TrySpendGold(refineVM.previewCost.Value))
            {
                var weapon = currentWeaponVM.Value;
                int oldRefineLevel = weapon.refineLevel.Value;
                bool wasCanRefine = !weapon.IsRefineMaxed();
                refineVM.ApplyRefine();
                int newRefineLevel = weapon.refineLevel.Value;
                bottomVM.isRefineMaxed.Value = weapon.IsRefineMaxed();
                requestCloseItemSelectPanel.OnNext(Unit.Default);
                requestPlayRefineResult.OnNext(new RefineResultData(oldRefineLevel, newRefineLevel, wasCanRefine != !weapon.IsRefineMaxed()));
                GameSaveCoordinator.Instance.MarkDirty();
            }
        }).AddTo(disposables);
        
        enhanceVM.requestOpenItemSelectPanel
            .Subscribe(requestOpenItemSelectPanel.OnNext)
            .AddTo(disposables);

        refineVM.requestOpenItemSelectPanel
            .Subscribe(requestOpenItemSelectPanel.OnNext)
            .AddTo(disposables);
        InitializeEnhancement();
        InitializeRefinement();
        InitializePromotion();
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
        currentTabIndex.Value = index;
    }
    
    public void Dispose()
    {
        DisposePromotion();
        DisposeRefinement();
        DisposeEnhancement();
        disposables.Dispose();
        MiddleVM.Dispose();
        infoVm.Dispose();
        enhanceVM.Dispose();
        refineVM.Dispose();
        bottomVM.Dispose();
    }
    
    static float GetExpProgress(EquipItemViewModel weapon)
    {
        if (weapon == null || weapon.Model == null || weapon.Model.LevelSystem == null)
            return 0f;

        int max = weapon.Model.EnhanceExpRequired(weapon.Model.Level);
        if (max <= 0)
            return 0f;

        return (float)weapon.Model.LevelSystem.CurrentExp / max;
    }
}

/// <summary>
/// 武器界面跳转参数
/// </summary>
public readonly struct EnhanceResultData
{
    public readonly int oldLevel;
    public readonly int newLevel;
    public readonly float oldProgress;
    public readonly float newProgress;
    public readonly int levelUpCount;
    public readonly bool needSwitchContent;
    public readonly Color rarityColor;

    public EnhanceResultData(int oldLevel, int newLevel, float oldProgress, float newProgress, int levelUpCount, bool needSwitchContent, Color rarityColor)
    {
        this.oldLevel = oldLevel;
        this.newLevel = newLevel;
        this.oldProgress = oldProgress;
        this.newProgress = newProgress;
        this.levelUpCount = levelUpCount;
        this.needSwitchContent = needSwitchContent;
        this.rarityColor = rarityColor;
    }
}

public readonly struct RefineResultData
{
    public readonly int oldRefineLevel;
    public readonly int newRefineLevel;
    public readonly bool isMaxRefineLevel;

    public RefineResultData(int oldRefineLevel, int newRefineLevel, bool isMaxRefineLevel)
    {
        this.oldRefineLevel = oldRefineLevel;
        this.newRefineLevel = newRefineLevel;
        this.isMaxRefineLevel = isMaxRefineLevel;
    }
}

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
