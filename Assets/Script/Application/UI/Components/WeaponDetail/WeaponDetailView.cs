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
public partial class WeaponDetailView : UIView
{
    //UIControlData
    
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
    
    WeaponDetailViewModel weaponDetailVM;

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
        //todo:view中不允许创建vm，放到类似context的地方
        var item = ItemFactory.CreateWeaponItem();
        equipItemVm = new EquipItemViewModel(item);
        var weapon = new ReactiveProperty<EquipItemViewModel>(equipItemVm);
        if (weaponDetailVM == null)
        {
            weaponDetailVM = new WeaponDetailViewModel(weapon,GameContext.Instance.InventoryRepository);
        } 
        
        Bind(weaponDetailVM);
        //子view绑定vm
        MiddleHub.Bind(weaponDetailVM.MiddleVM);
        infoPanelView.Bind(weaponDetailVM.infoVm);
        enhancePanelView.Bind(weaponDetailVM.enhanceVM);
     
        weaponDetailVM.SetWeapon(equipItemVm);
    }


    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public void Bind(WeaponDetailViewModel viewModel)
    {
        weaponDetailVM = viewModel;
        
        weaponDetailVM.currentWeaponVM.Subscribe(weapon =>
        {
            OnWeaponChanged(weapon);
        }).AddTo(this);
    }
    
    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        //todo:view中不允许创建vm，放到类似context的地方
        var item = ItemFactory.CreateWeaponItem();
        equipItemVm = new EquipItemViewModel(item);
        var weapon = new ReactiveProperty<EquipItemViewModel>(equipItemVm);
        if (weaponDetailVM == null)
        {
            weaponDetailVM = new WeaponDetailViewModel(weapon,GameContext.Instance.InventoryRepository);
        } 
        
        Bind(weaponDetailVM);
        //子view绑定vm
        MiddleHub.Bind(weaponDetailVM.MiddleVM);
        infoPanelView.Bind(weaponDetailVM.infoVm);
        enhancePanelView.Bind(weaponDetailVM.enhanceVM);
        refinePanelView.Bind(weaponDetailVM.refineVM);
        bottomView.Bind(weaponDetailVM.bottomVM);
        
        weaponDetailVM.SetWeapon(equipItemVm);
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
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
