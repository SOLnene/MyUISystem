using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.UI.Components.CharacterDetail;
using UnityEngine;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine.UI;
public class EnhancePanelView : MonoBehaviour
{
    [SerializeField]
    GameObject upgradePanel;
    [SerializeField]
    GameObject breakOutPanel;
    
    [Header("可升级情况界面")]
    [SerializeField]
    GameObject unMaxLevelPanel;
    [SerializeField]
    TextMeshProUGUI levelValueText;
    [SerializeField]
    List<GameObject> arrows;

    [SerializeField]
    TextMeshProUGUI expValueText;
    [SerializeField]
    TextMeshProUGUI expPlusValueText;
    [SerializeField]
    EnhancePanelExpBar expBar;
    [SerializeField]
    EnhanceLevelPreviewView enhanceLevelPreviewView;
    [SerializeField]
    PromoteLevelPreviewView promoteLevelPreviewView;
    [Header("通用")]
    [SerializeField]
    StatItemView[] statItemViews;
    
    [Space]
    [Header("右下面板")]
    [SerializeField]
    EnhanceRightBottomView rightBottomView;
    
    EnhancePanelViewModel vm;

    CompositeDisposable rootDisposable = new();
    
    public void Bind(EnhancePanelViewModel viewModel)
    {
        vm = viewModel;
        
        expBar.BindData();
        BindStatItems();
        BindEnhancePreview();
        BindPromotePreview();
        
        viewModel.weaponVM
            .Where(w => w != null)
            .Subscribe(weapon =>
            {
                BindUpgradeUI(weapon);
                weapon.needBreak.Subscribe(b =>
                {
                    SwitchPreviewMode(b).Forget();
                }).AddTo(rootDisposable);

            })
            .AddTo(rootDisposable);
        
        
        rightBottomView.Bind(vm.rightBottomVM);
    }

    void BindStatItems()
    {
        statItemViews[0].Bind(vm.statItemVMs[0]);
        statItemViews[1].Bind(vm.statItemVMs[1]);
    }

    void BindEnhancePreview()
    {
        if (enhanceLevelPreviewView == null)
            return;

        enhanceLevelPreviewView.Bind(vm.enhanceLevelPreviewVm);
    }
    
    void BindPromotePreview()
    {
        if (promoteLevelPreviewView == null)
            return;

        promoteLevelPreviewView.Bind(vm.promotePreviewVm);
    }

    void BindUpgradeUI(EquipItemViewModel weapon)
    {
        weapon.level.Subscribe(value =>
        {
            if (levelValueText)
                levelValueText.text = $"Lv.{value}";
        }).AddTo(rootDisposable);
    
        Observable.CombineLatest(weapon.currentExp, weapon.nextLevelExp, 
                (cur, next) => new { cur, next })
            .Subscribe(exp =>
            {
                if (expValueText)
                    expValueText.text = $"{exp.cur}/{exp.next}";
            }).AddTo(rootDisposable);
    
        //经验条绑定
        Observable
            .CombineLatest(
                weapon.currentExp.StartWith(weapon.currentExp.Value),
                weapon.nextLevelExp.StartWith(weapon.nextLevelExp.Value),
                vm.previewExp.StartWith(vm.previewExp.Value),
                (cur, next,previewExp) => new { cur, next,previewExp })
            .Subscribe(exp =>
            {
                if (expBar)
                    expBar.SetValue(exp.cur, exp.next,exp.cur+exp.previewExp);
            })
            .AddTo(rootDisposable);
        
        vm.rightBottomVM.totalExp.Subscribe(exp =>
        {
            expPlusValueText.text = $"+{exp}";
        }).AddTo(rootDisposable);
    }
    
    void ShowEnhancePreview()
    {
        if (enhanceLevelPreviewView != null)
        {
            enhanceLevelPreviewView.Show().Forget();
        }
    }
    
    void ShowPromotePreview()
    {
        if (promoteLevelPreviewView != null)
        {
            promoteLevelPreviewView.Show().Forget();
        }
    }

    async UniTask SwitchPreviewMode(bool isPromote)
    {
        if (isPromote)
        {
            if (upgradePanel != null)
                upgradePanel.SetActive(false);
            if (breakOutPanel != null)
                breakOutPanel.SetActive(true);

            ShowPromotePreview();
            return;
        }

        if (promoteLevelPreviewView != null && promoteLevelPreviewView.gameObject.activeInHierarchy)
        {
            await promoteLevelPreviewView.Hide();
        }

        if (breakOutPanel != null)
            breakOutPanel.SetActive(false);
        if (upgradePanel != null)
            upgradePanel.SetActive(true);
    }
    
    void OnDestroy()
    {
        rootDisposable.Dispose();
    }
    
}
