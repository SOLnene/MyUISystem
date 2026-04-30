using System;
using System.Collections;
using System.Collections.Generic;
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
    private BackpackTopView TopHub;
    [ControlBinding]
    private WeaponDetailMiddleView MiddleHub;

		#pragma warning restore 0649
#endregion


    //private ReactiveProperty<WeaponItem> weaponItem = new ReactiveProperty<WeaponItem>();

    [Header("具体界面")]
    [SerializeField]
    InfoPanelView infoPanelView;
    [SerializeField]
    EnhancePanelView enhancePanelView;
    [SerializeField]
    RefinePanelView refinePanelView;
    [SerializeField]
    WeaponDetailBottomView bottomView;
    
    EquipDetailViewModel equipDetailVm;

    EquipItemViewModel equipItemVm;
    
    /// <summary>
    /// 测试
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    void Start()
    {
        return;
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if (ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
        //OnOpen(ItemFactory.CreateWeaponItem());
        //todo:放到类似context的地方
        var item = ItemFactory.CreateWeaponItem();
        equipItemVm = new EquipItemViewModel(item);
        var weapon = new ReactiveProperty<EquipItemViewModel>(equipItemVm);
        if (equipDetailVm == null)
        {
            equipDetailVm = new EquipDetailViewModel(weapon,GameContext.Instance.InventoryRepository);
        } 
        
        Bind(equipDetailVm);
        //子view绑定vm
        MiddleHub.Bind(equipDetailVm.MiddleVM);
        infoPanelView.Bind(equipDetailVm.infoVm);
        enhancePanelView.Bind(equipDetailVm.enhanceVM);
     
        equipDetailVm.SetWeapon(equipItemVm);
    }


    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

   
    
    public override void OnOpen(object data)
    {
        base.OnOpen(data);
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
    }

    public void Bind(EquipDetailViewModel viewModel)
    {
        equipDetailVm = viewModel;
        
        equipDetailVm.currentWeaponVM.Subscribe(weapon =>
        {
            OnWeaponChanged(weapon);
        }).AddTo(this);
    }
    
    void OnWeaponChanged(EquipItemViewModel viewModel)
    {
    if (viewModel == null)
        {
            return;
        }
        TopHub.SetTitle(viewModel.Model.ItemName);
    }
    
    
    public override void OnAddListener()
    {
        base.OnAddListener();
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        base.OnClose();
        equipDetailVm?.Dispose();
        equipDetailVm = null;
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
