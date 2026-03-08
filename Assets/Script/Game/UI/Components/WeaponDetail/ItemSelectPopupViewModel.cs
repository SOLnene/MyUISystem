using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using Unity.VisualScripting;

public class ItemSelectPopupViewModel
{
    public readonly ReactiveCollection<ItemSlotViewModel> candidateSlots = new();
    //public readonly ReactiveCollection<ItemSlotViewModel> selectedSlots = new();
    /// <summary>
    /// 最后选中的槽位，用于信息面板显示
    /// </summary>
    public readonly ReactiveProperty<ItemSlotViewModel> lastSelctedSlot = new();
    
    MaterialSelectParams materialSelectParams;
    
    public InfoPanelViewModel infoPanelViewModel = new InfoPanelViewModel();

    MaterialSelectService selectService;
    
    CompositeDisposable disposable = new();

    public ItemSelectPopupViewModel()
    {
       
    }
    
    public void Initialize(MaterialSelectParams param)
    {
        materialSelectParams = param;
        candidateSlots.Clear();
        //selectedSlots.Clear();
        lastSelctedSlot.Value = null;
        selectService = param.service;
        
        //重新打开时同步状态
        selectService.OnDelta.Subscribe(delta =>
        {
            var slot = candidateSlots.FirstOrDefault(s => s.ItemViewModel.Model == delta.item);
            if (slot != null)
            {
                slot.isChecked.Value = delta.added;
            }
        }).AddTo(disposable);
        var allItems = GameContext.Instance.InventoryRepository.GetAllItems();
        foreach (var item in allItems)
        {
            if (param.filter.Match(item))
            {
                var slotVM = new ItemSlotViewModel(item);
                candidateSlots.Add(slotVM);
                
                //同步选择状态
                slotVM.isChecked.Value = selectService.Contains(item);
                
                slotVM.onClick.Subscribe(_=>
                {
                    if (lastSelctedSlot.Value == slotVM && slotVM.isChecked.Value)
                        return;
                    // 取消上一个选中
                    if (lastSelctedSlot.Value != null)
                        lastSelctedSlot.Value.isSelected.Value = false;
                    lastSelctedSlot.Value = slotVM;
                    slotVM.isSelected.Value = true; 
                    
                    if (!slotVM.isChecked.Value)
                    {
                        selectService.TrySelect(slotVM.ItemViewModel.Model);
                        //selectedSlots.Add(slotVM);
                        slotVM.isChecked.Value = true;
                    }
                }).AddTo(disposable);
                slotVM.onRemove.Subscribe(_ =>
                {
                    selectService.TryDeselect(slotVM.ItemViewModel.Model);
                    //selectedSlots.Remove(slotVM);
                    slotVM.isChecked.Value = false;
                }).AddTo(disposable);
            }
        }
        
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}

// 一个通用事件负载类型（用于命令总线）
public class MaterialSelectResult
{
    public int slotIndex;
    public List<ItemSlotViewModel> selectedItems;
}

// 打开选择面板时的参数
public class MaterialSelectParams
{
    public int slotIndex;
    public ItemFilter filter;
    public int maxCount;
    public MaterialSelectService service;
    
    public MaterialSelectParams(int slotIndex =0, ItemFilter filter = null, int maxCount = 40, MaterialSelectService service = null)
    {
        this.slotIndex = slotIndex;
        this.filter = filter ?? new ItemFilter(ItemCategory.All);
        this.maxCount = maxCount;
        this.service = service;
    }
}

public class ItemFilter
{
    ItemCategory category;
    int starLimit;
    int id;
    public ItemFilter(ItemCategory itemCategory,int star = 1,int id = 0)
    {
        category = itemCategory;
        starLimit = star;
        this.id = id;
    }
    
    public bool Match(InventoryItem item)
    {
        return category == item.Category && item.Stars<= starLimit && (id == 0 || item.Id == id);
    }
}