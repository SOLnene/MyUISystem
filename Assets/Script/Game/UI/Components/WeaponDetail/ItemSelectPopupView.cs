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
    //实际使用
    ItemSlotView slotPrefab;

    // 全屏点击遮罩
    [SerializeField]
    Button clickHandler;

    //是否显示infopanel,先这样写 
    bool showInfopanel;
    CompositeDisposable disposable = new();
    
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
        
        slotPrefab = UIManager.Instance.slotPrefab;
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
        vm.candidateSlots.ObserveAdd().Subscribe(async add =>
        {
            CreateSlot(add.Value);
        }).AddTo(disposable);

        vm.candidateSlots.ObserveRemove().Subscribe(rem =>
        {
            var slotView = activeItemSlots.Find(s => s.vm == rem.Value);
            if (slotView != null)
            {
                activeItemSlots.Remove(slotView);
                Destroy(slotView.gameObject);
            }
        }).AddTo(disposable);
        infoPanelView.Bind(vm.infoPanelViewModel);
        //todo:这个应该由外部传入
        infoPanelView.gameObject.SetActive(showInfopanel);
        if (showInfopanel)
        {
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

    void CreateSlot(ItemSlotViewModel viewModel)
    {
        var slotView = GameObject.Instantiate(slotPrefab, Content);
        activeItemSlots.Add(slotView);
        slotView.Bind(viewModel);
    }
    
    async UniTaskVoid CreateSlotAsync(ItemSlotViewModel viewModel)
    {
        try
        {
            var slotView = await ItemFactory.InstantiateItemSlot(viewModel, Content);
            activeItemSlots.Add(slotView);
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
        foreach (var slot in activeItemSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
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
