using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;

/// <summary>
/// 同时作为v,vm
/// </summary>
public partial class EquipDetailView : UIView
{
    //UIControlData
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private WeaponDetailMiddleView MiddleHub;

		#pragma warning restore 0649
#endregion


    //private ReactiveProperty<WeaponItem> weaponItem = new ReactiveProperty<WeaponItem>();

    [SerializeField]
    UITopBar topArea;
    [Header("具体界面")]
    [SerializeField]
    InfoPanelView infoPanelView;
    [SerializeField]
    EnhancePanelView enhancePanelView;
    [SerializeField]
    RefinePanelView refinePanelView;
    [SerializeField]
    WeaponDetailBottomView bottomView;
    [SerializeField]
    UITransitionGroup pageTransition;
    [SerializeField]
    ItemSelectPanelView itemSelectPanelView;
    //参考图
    [Header("参考图")]
    [SerializeField]
    GameObject[] finalImages;
    EquipDetailViewModel equipDetailVm;

    EquipItemViewModel equipItemVm;
    readonly CompositeDisposable disposable = new CompositeDisposable();
    int currentTabIndex = -1;
    bool isSwitchingTab;
    bool isPlayingResultFlow;
    bool isClosing;

    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }
    
    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        isClosing = false;
        //todo:view中不允许创建vm，放到类似context的地方
        
        var param = data as EquipDetailOpenParams;
        if (param == null)
        {
            Debug.LogError("缺少武器界面参数");
        }
        else
        {
            equipItemVm = param.Weapon;
        }
        
        var weapon = new ReactiveProperty<EquipItemViewModel>(equipItemVm);
        
        //不复用，每次打开重新创建
        equipDetailVm?.Dispose();
        equipDetailVm = new EquipDetailViewModel(weapon,GameContext.Instance.InventoryRepository);
        
        Bind(equipDetailVm);
        //子view绑定vm
        MiddleHub.Bind(equipDetailVm.MiddleVM);
        infoPanelView.Bind(equipDetailVm.infoVm);
        enhancePanelView.Bind(equipDetailVm.enhanceVM);
        refinePanelView.Bind(equipDetailVm.refineVM);
        bottomView.Bind(equipDetailVm.bottomVM);
        
        equipDetailVm.ApplyOpenParams(param);
        ApplyTabImmediate(equipDetailVm.currentTabIndex.Value);
        BindTabFlow();
        foreach (var img in finalImages)
        {
            img.SetActive(false);
        }
        
        if (itemSelectPanelView != null)
        {
            itemSelectPanelView.Hide();
        }

        pageTransition?.Show().Forget();
    }

    public void Bind(EquipDetailViewModel viewModel)
    {
        disposable.Clear();
        equipDetailVm = viewModel;

        if (equipDetailVm == null)
        {
            return;
        }

        equipDetailVm.currentWeaponVM
            .Where(weapon => weapon != null)
            .Subscribe(OnWeaponChanged)
            .AddTo(disposable);

        topArea.Bind(
            equipItemVm.Model.ItemName,
            GameEconomy.Instance.gold,
            OnCancel
            );
        
        equipDetailVm.requestOpenItemSelectPanel
            .Subscribe(param =>
            {
                itemSelectPanelView.Show(param);
            })
            .AddTo(disposable);
        
        equipDetailVm.requestCloseItemSelectPanel
            .Subscribe(_ =>
            {
                itemSelectPanelView.Hide();
            })
            .AddTo(disposable);
    }

    //todo:放这里是为了统一管理动画，但我不知道是否需要这样，毕竟我可以手动控制两个动画时常相同
    void BindTabFlow()
    {
        equipDetailVm.currentTabIndex
            .Skip(1)
            .Subscribe(_ => SwitchContent().Forget())
            .AddTo(disposable);

        equipDetailVm.requestRefreshContentWithAnimation
            .Subscribe(_ => SwitchContent().Forget())
            .AddTo(disposable);

        equipDetailVm.requestPlayEnhanceResult
            .Subscribe(result => PlayEnhanceResultFlow(result).Forget())
            .AddTo(disposable);

        equipDetailVm.requestPlayPromoteResult
            .Subscribe(_ => PlayPromoteResultFlow().Forget())
            .AddTo(disposable);
    }

    void ApplyTabImmediate(int index)
    {
        currentTabIndex = index;
        RefreshContent();
        MiddleHub.ShowImmediate();
        bottomView.ShowImmediate();
    }

    async UniTask SwitchContent()
    {
        if (isSwitchingTab)
            return;

        isSwitchingTab = true;
        try
        {
            await HideTabContent();
            RefreshContent();
            await ShowTabContent();
        }
        finally
        {
            isSwitchingTab = false;
        }
    }

    async UniTask PlayEnhanceResultFlow(EnhanceResultData result)
    {
        if (isSwitchingTab || isPlayingResultFlow)
            return;

        isPlayingResultFlow = true;
        try
        {
            if (enhancePanelView != null)
                await enhancePanelView.PlayEnhanceResult(result);
        }
        finally
        {
            isPlayingResultFlow = false;
        }

        if (result.needSwitchContent)
            await SwitchContent();
    }

    async UniTask PlayPromoteResultFlow()
    {
        if (isSwitchingTab || isPlayingResultFlow)
            return;

        isPlayingResultFlow = true;
        try
        {
            if (enhancePanelView != null)
                await enhancePanelView.PlayPromoteResult();
        }
        finally
        {
            isPlayingResultFlow = false;
        }

        await SwitchContent();
    }

    async UniTask HideTabContent()
    {
        await UniTask.WhenAll(
            MiddleHub.HideContent(),
            bottomView.HideContent());
    }

    void RefreshContent()
    {
        currentTabIndex = equipDetailVm.currentTabIndex.Value;
        MiddleHub.Refresh();
        bottomView.Refresh();
    }

    async UniTask ShowTabContent()
    {
        await UniTask.WhenAll(
            MiddleHub.ShowContent(),
            bottomView.ShowContent());
    }
    
    void OnWeaponChanged(EquipItemViewModel viewModel)
    {
    if (viewModel == null)
        {
            return;
        }
        //TopHub.SetTitle(viewModel.Model.ItemName);
    }
    
  
   public override void OnAddListener()
   {
       base.OnAddListener();
   
       /*if (TopHub != null)
       {
           TopHub.OnBackClicked += OnTopBackClicked;
       }*/
   }
   
   public override void OnRemoveListener()
   {
       /*if (TopHub != null)
       {
           TopHub.OnBackClicked -= OnTopBackClicked;
       }*/
   
       base.OnRemoveListener();
   }
   
   void OnTopBackClicked()
   {
       OnCancel();
   }

    public override void OnCancel()
    {
        if (isClosing)
            return;

        CloseWithTransition().Forget();
    }

    async UniTask CloseWithTransition()
    {
        isClosing = true;

        if (pageTransition != null)
        {
            await pageTransition.Hide();
        }

        base.OnCancel();
    }

    public override void OnClose()
    {
        base.OnClose();
        disposable.Clear();
        equipDetailVm?.Dispose();
        equipDetailVm = null;
        currentTabIndex = -1;
        isSwitchingTab = false;
        isPlayingResultFlow = false;
        isClosing = false;
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
