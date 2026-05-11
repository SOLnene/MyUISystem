using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.UI.Components.CharacterDetail;
using UnityEngine;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine.Serialization;
using UnityEngine.UI;
public class EnhancePanelView : MonoBehaviour
{
    [SerializeField]
    GameObject enhancePanel;
    [SerializeField]
    GameObject promotePanel;
    [SerializeField]
    AnimatedPanel animatedPanelRoot;
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
        rootDisposable.Clear();
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
        int count = Mathf.Min(statItemViews.Length, vm.statItemVMs.Length);
        for (int i = 0; i < count; i++)
        {
            if (statItemViews[i] != null)
                statItemViews[i].Bind(vm.statItemVMs[i]);
        }
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
        Debug.Log("切换预览模式，是否晋升："+isPromote);
        return isPromote ? SwitchToPromoteMode() : SwitchToEnhanceMode();
    }
    
    async UniTask SwitchToPromoteMode()
    {
        vm.RefreshPromoteMaterialPreview();
        if (promoteMaterialPreviewView != null)
            await promoteMaterialPreviewView.Bind(vm.promoteMaterialPreviewVm);

        await HideContent();
        SetPanelActive(enhancePanel, false);
        SetPanelActive(promotePanel, true);
        await ShowContent();
    }
    
    async UniTask SwitchToEnhanceMode()
    {
        await HideContent();
        SetPanelActive(promotePanel, false);
        SetPanelActive(enhancePanel, true);
        await ShowContent();
    }

    async UniTask HideContent()
    {
        if (animatedPanelRoot != null)
        {
            await animatedPanelRoot.Show(true);
            await animatedPanelRoot.Hide();
        }
    }

    async UniTask ShowContent()
    {
        if (animatedPanelRoot != null)
            await animatedPanelRoot.Show();
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
