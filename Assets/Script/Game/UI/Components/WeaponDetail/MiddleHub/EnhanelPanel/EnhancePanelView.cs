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
    
    [SerializeField]
    EnhanceLevelPreviewView enhanceLevelPreviewView;
    [SerializeField]
    PromoteLevelPreviewView promoteLevelPreviewView;
    [Header("通用")]
    [SerializeField]
    StatItemView[] statItemViews;
    
    [SerializeField]
    GameObject enhanceMaterialPreviewView;
    [SerializeField]
    PromoteMaterialPreviewView promoteMaterialPreviewView;
    [Space]
    [Header("右下面板")]
    [SerializeField]
    EnhanceRightBottomView rightBottomView;
    
    EnhancePanelViewModel vm;

    CompositeDisposable rootDisposable = new();
    
    public void Bind(EnhancePanelViewModel viewModel)
    {
        vm = viewModel;
        
        BindStatItems();
        BindEnhancePreview();
        BindPromotePreview();
        //todo：不一定要考虑武器切换
        viewModel.weaponVM
            .Where(w => w != null)
            .Subscribe(weapon =>
            {
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
    
    UniTask SwitchPreviewMode(bool isPromote)
    {
        return isPromote ? SwitchToPromoteMode() : SwitchToEnhanceMode();
    }
    
    async UniTask SwitchToPromoteMode()
    {
        if (enhanceLevelPreviewView != null && enhanceLevelPreviewView.gameObject.activeInHierarchy)
            await enhanceLevelPreviewView.Hide();

        SetPanelActive(upgradePanel, false);
        SetPanelActive(breakOutPanel, true);
        SetPanelActive(enhanceMaterialPreviewView, false);
        SetPanelActive(promoteMaterialPreviewView, true);

        vm.RefreshPromoteMaterialPreview();
        if (promoteMaterialPreviewView != null)
            await promoteMaterialPreviewView.Bind(vm.promoteMaterialPreviewVm);

        if (promoteLevelPreviewView != null)
            await promoteLevelPreviewView.Show();
    }
    
    async UniTask SwitchToEnhanceMode()
    {
        if (promoteLevelPreviewView != null && promoteLevelPreviewView.gameObject.activeInHierarchy)
        {
            await promoteLevelPreviewView.Hide();
        }

        SetPanelActive(breakOutPanel, false);
        SetPanelActive(upgradePanel, true);
        SetPanelActive(promoteMaterialPreviewView, false);
        SetPanelActive(enhanceMaterialPreviewView, true);

        if (enhanceLevelPreviewView != null)
            await enhanceLevelPreviewView.Show();
    }
    
    void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    void SetPanelActive(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
    }
    
    void OnDestroy()
    {
        rootDisposable.Dispose();
    }
    
}
