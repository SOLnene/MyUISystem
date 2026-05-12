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

    void BindTabFlow()
    {
        equipDetailVm.currentTabIndex
            .Skip(1)
            .Subscribe(index => SwitchTab(index).Forget())
            .AddTo(disposable);
    }

    void ApplyTabImmediate(int index)
    {
        currentTabIndex = index;
        MiddleHub.ApplyTabImmediate(index);
        bottomView.SetTabContent(index);
        bottomView.ShowImmediate();
    }

    async UniTask SwitchTab(int nextIndex)
    {
        if (isSwitchingTab || nextIndex == currentTabIndex)
            return;

        isSwitchingTab = true;
        try
        {
            await HideTabContent();
            ApplyTabContent(nextIndex);
            await ShowTabContent();
            currentTabIndex = nextIndex;
        }
        finally
        {
            isSwitchingTab = false;
        }
    }

    async UniTask HideTabContent()
    {
        await UniTask.WhenAll(
            MiddleHub.HideContent(),
            bottomView.HideContent());
    }

    void ApplyTabContent(int nextIndex)
    {
        MiddleHub.SetPanelActive(currentTabIndex, false);
        bottomView.SetTabContent(nextIndex);
        MiddleHub.SetPanelActive(nextIndex, true);
        MiddleHub.SetTabSelected(nextIndex);
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
        isClosing = false;
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
