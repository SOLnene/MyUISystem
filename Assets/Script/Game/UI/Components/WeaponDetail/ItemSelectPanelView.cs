using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

//物品选择界面的内嵌版本
public class ItemSelectPanelView : MonoBehaviour
{
    ItemSelectPopupViewModel vm;
    readonly List<ItemSlotView> activeItemSlots = new();

    [SerializeField]
    Transform content;
    [SerializeField]
    InfoPanelView infoPanelView;
    // 全屏点击遮罩
    [SerializeField]
    Button clickHandler;
    //右侧信息面板的关闭遮罩
    [SerializeField]
    Button infoPanelCloseHandler;
    
    
    const string slotPrefabAddress = "ui/prefab/item_slot_itemselect";

    bool showInfopanel;
    readonly CompositeDisposable disposable = new();
    int slotCreateVersion;

    public ItemSelectPopupViewModel ViewModel => vm;
    
    public void Show(object data)
    {
        gameObject.SetActive(true);
        //transform.SetAsLastSibling();
        disposable.Clear();
        vm?.Dispose();

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
            clickHandler.onClick.AddListener(OnClickHandlerClicked);
        }
        
        if(infoPanelCloseHandler != null)
        {
            infoPanelCloseHandler.onClick.RemoveAllListeners();
            infoPanelCloseHandler.onClick.AddListener(CloseInfoPanel);
        }
    }

    public void Hide()
    {
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
        if (clickHandler != null)
        {
            clickHandler.onClick.RemoveAllListeners();
        }
        
        if (infoPanelView != null)
        {
            infoPanelView.gameObject.SetActive(false);
        }

        if (infoPanelCloseHandler != null)
        {
            infoPanelCloseHandler.onClick.RemoveAllListeners();
            infoPanelCloseHandler.gameObject.SetActive(false);
        }
        
        gameObject.SetActive(false);
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
                        infoPanelView.gameObject.SetActive(true);
                        if(infoPanelCloseHandler != null)
                        {
                            infoPanelCloseHandler.gameObject.SetActive(showInfopanel);
                        }
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
        infoPanelView.gameObject.SetActive(false);
        if (infoPanelCloseHandler != null)
        {
            infoPanelCloseHandler.gameObject.SetActive(false);
        }
    }
    
    void OnClickHandlerClicked()
    {
        vm?.onCancel?.Invoke();
        Hide();
    }
}
