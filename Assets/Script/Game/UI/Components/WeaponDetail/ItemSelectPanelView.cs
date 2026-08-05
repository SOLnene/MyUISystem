using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

//物品选择界面的内嵌版本
public class ItemSelectPanelView : SelectionPanelView
{
    ItemSelectPopupViewModel vm;
    readonly List<ItemSlotView> activeItemSlots = new();

    [SerializeField]
    InfoPanelView infoPanelView;
    //右侧信息面板的关闭遮罩
    [SerializeField]
    Button infoPanelCloseHandler;
    
    const string slotPrefabAddress = "ui/prefab/item_slot_itemselect";

    bool showInfopanel;

    public ItemSelectPopupViewModel ViewModel => vm;
    
    protected override void OnShow(object data)
    {
        vm?.Dispose();

        vm = new ItemSelectPopupViewModel();

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

        if(infoPanelCloseHandler != null)
        {
            infoPanelCloseHandler.onClick.RemoveAllListeners();
            infoPanelCloseHandler.onClick.AddListener(CloseInfoPanel);
        }
    }

    /*1. 先 slotCreateVersion++，阻止异步创建 slot 回来
    2. 先移除点击监听，避免关闭中重复点击
    3. 播 ItemSelectPanel 的退出动画
    4. 动画结束后再回收 slot / 清订阅 / 关子面板*/
    protected override void OnBeforeHide()
    {
        if (infoPanelCloseHandler != null)
        {
            infoPanelCloseHandler.onClick.RemoveAllListeners();
            infoPanelCloseHandler.gameObject.SetActive(false);
        }
    }

    protected override void OnHidden()
    {
        foreach (var slot in activeItemSlots)
        {
            if (slot != null)
            {
                ResourceManager.Instance.Recycle(slot.gameObject);
            }
        }
        activeItemSlots.Clear();
        disposable.Clear();
          
        if (infoPanelView != null)
        {
            infoPanelView.gameObject.SetActive(false);
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

        vm.requestTip.Subscribe(text =>
        {
            UIManager.Instance.Open(UIType.TipView, text);
        }).AddTo(disposable);

        if (infoPanelView != null)
        {
            infoPanelView.gameObject.SetActive(showInfopanel);
            if(infoPanelCloseHandler != null)
            {
                infoPanelCloseHandler.gameObject.SetActive(showInfopanel);
            }
            if (showInfopanel)
            {
                infoPanelView.Bind(vm.infoPanelViewModel);
                vm.lastSelctedSlot.Subscribe(slot =>
                {
                    if (slot == null)
                    {
                        infoPanelView.gameObject.SetActive(false);
                        if(infoPanelCloseHandler != null)
                        {
                            infoPanelCloseHandler.gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        if(infoPanelCloseHandler != null)
                        {
                            infoPanelCloseHandler.gameObject.SetActive(showInfopanel);
                        }
                        infoPanelView.Show(slot.ItemViewModel);
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
            var slotView = await ItemFactory.InstantiateItemSlot(viewModel, content, slotPrefabAddress);
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
            Debug.LogError("[ItemSelectPanelView.CreateSlotAsync] 创建ItemSlot失败: " + e);
        }
    }

    void CloseInfoPanel()
    {
        infoPanelView.Hide();
        if (infoPanelCloseHandler != null)
        {
            infoPanelCloseHandler.gameObject.SetActive(false);
        }
    }
    
    protected override void OnCancelRequested()
    {
        vm?.onCancel?.Invoke();
    }
}
