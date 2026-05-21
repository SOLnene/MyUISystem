using System;
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

    public void Refresh()
    {
        if (vm == null || vm.weaponVM.Value == null)
            return;

        bool isPromote = vm.weaponVM.Value.needBreak.Value;
        if (isPromote)
        {
            vm.RefreshPromoteMaterialPreview();
            if (promoteMaterialPreviewView != null)
                promoteMaterialPreviewView.Bind(vm.promoteMaterialPreviewVm).Forget();
        }
        else if (enhanceLevelPreviewView != null)
        {
            enhanceLevelPreviewView.Refresh();
        }

        SetPanelActive(enhancePanel, !isPromote);
        SetPanelActive(promotePanel, isPromote);
    }

    public async UniTask PlayEnhanceExpProgress(EnhanceResultData result)
    {
        if (enhanceLevelPreviewView != null)
            await enhanceLevelPreviewView.PlayExpProgress(result);
    }

    public async UniTask PlayEnhanceLevelResult(EnhanceResultData result, Action onNewLevelShown = null)
    {
        if (enhanceLevelPreviewView != null)
            await enhanceLevelPreviewView.PlayLevelResult(result, onNewLevelShown);
    }

    public void ShowEnhanceProcessing()
    {
        if (rightBottomView != null)
            rightBottomView.ShowProcessing();
    }

    public void ShowEnhanceNormal(bool playMaterialContentFx)
    {
        if (rightBottomView != null)
            rightBottomView.ShowNormal(playMaterialContentFx);
    }

    public void ShowEnhanceMaxLevelText(string text)
    {
        if (rightBottomView != null)
            rightBottomView.ShowMaxLevelText(text);
    }

    public async UniTask PlayPromoteResult()
    {
        await UniTask.Delay(500);
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
