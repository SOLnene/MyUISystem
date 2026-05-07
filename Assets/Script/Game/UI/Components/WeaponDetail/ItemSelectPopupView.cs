using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;


public partial class ItemSelectPopupView : UIView
{
    ItemSelectPopupViewModel vm;
    List<ItemSlotView> activeItemSlots = new List<ItemSlotView>();

    [SerializeField]
    InfoPanelView infoPanelView;
    const string slotPrefabAddress = "ui/prefab/item_slot_itemselect";

    // 全屏点击遮罩
    [SerializeField]
    Button clickHandler;

    [SerializeField]
    AnimatedPanel animatedPanel;
    
    //是否显示infopanel,先这样写 
    bool showInfopanel;
    CompositeDisposable disposable = new();
    /// <summary>
    /// 版本控制，保证异步创建的槽位不会被过时的版本添加到界面上，以及界面关闭时不会添加槽位
    /// </summary>
    int slotCreateVersion;
    
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        disposable.Clear();
        vm?.Dispose(); // 确保旧订阅释放
        
        vm = new ItemSelectPopupViewModel();
        slotCreateVersion++;
        
        if (data is SinglePickParams singlePickParams)
        {
            showInfopanel = false;
            Bind(vm);
            vm.Initialize(singlePickParams);
        }
        else if (data is MaterialSelectParams materialSelectParams)
        {
            showInfopanel = true;
            Bind(vm);
            vm.Initialize(materialSelectParams);
        }
        else
        {
            Debug.LogError("ItemSelectPopupView 参数错误");
        }
        if (clickHandler != null)
        {
            clickHandler.onClick.RemoveAllListeners();
            clickHandler.onClick.AddListener(() =>
            {
                vm.onCancel?.Invoke();
                UIManager.Instance.Close(UIType.ItemSelectPopupView);
            });
        }
        
    }

    public void Bind(ItemSelectPopupViewModel viewModel)
    {
        vm = viewModel;
        vm.candidateSlots.ObserveAdd().Subscribe(add =>
        {
            CreateSlotAsync(add.Value).Forget();
        }).AddTo(disposable);

        vm.candidateSlots.ObserveRemove().Subscribe(rem =>
        {
            var slotView = activeItemSlots.Find(s => s.vm == rem.Value);
            if (slotView != null)
            {
                activeItemSlots.Remove(slotView);
                ResourceManager.Instance.Recycle(slotView.gameObject);
            }
        }).AddTo(disposable);
        //infoPanelView.Bind(vm.infoPanelViewModel);
        //todo:这个应该由外部传入
        if (infoPanelView != null)
        {
            infoPanelView.gameObject.SetActive(showInfopanel);
            if (showInfopanel)
            {
                infoPanelView.Bind(vm.infoPanelViewModel);
                vm.lastSelctedSlot.Subscribe(slot =>
                {
                    if (slot == null)
                    {
                        infoPanelView.gameObject.SetActive(false);
                    }
                    else
                    {
                        infoPanelView.gameObject.SetActive(true);
                        vm.infoPanelViewModel.Bind(slot.ItemViewModel);
                    }
                }).AddTo(disposable);
            }
        }

    }

    async UniTask CreateSlotAsync(ItemSlotViewModel viewModel)
    {
        int version = slotCreateVersion;
        try
        {
            var slotView = await ItemFactory.InstantiateItemSlot(viewModel, Content, slotPrefabAddress);
            if (slotView == null)
            {
                return;
            }
            if (version != slotCreateVersion)
            {
                ResourceManager.Instance.Recycle(slotView.gameObject);
                return;
            }
            activeItemSlots.Add(slotView);
            slotView.Bind(viewModel);
        }
        catch (Exception e)
        {
            Debug.LogError("[ItemSelectPopupView.CreateSlotAsync] 创建ItemSlot失败: " + e);
        }
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
        slotCreateVersion++;
        foreach (var slot in activeItemSlots)
        {
            if (slot != null)
            {
                ResourceManager.Instance.Recycle(slot.gameObject);
            }
        }
        activeItemSlots.Clear();
        disposable.Clear();
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
