using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class BackpackMiddleView : MonoBehaviour
{
    [SerializeField]
    Transform slotParent;

    [SerializeField]
    ItemSlotView slotPrefab;

    BackpackMiddleViewModel middleVM;

    readonly List<ItemSlotView> activeSlots = new();
    
    InventoryItem boundItem;
    
    public void Bind(BackpackMiddleViewModel vm)
    {
        middleVM = vm;
        
        vm.displaySlots.ObserveAdd().Subscribe(add =>
        {
            CreateSlot(add.Value);
        }).AddTo(this);

        vm.displaySlots.ObserveRemove().Subscribe(rem =>
        {
            var view = activeSlots.Find(v => v.vm == rem.Value);
            if (view != null)
            {
                Destroy(view);
                activeSlots.Remove(view);
            }
        }).AddTo(this);
            
        
        vm.displaySlots.ObserveReset().Subscribe(x =>
        {
            //TODO:换用对象池缓存
            foreach (Transform child in slotParent)
            {
                Destroy(child.gameObject);
            }
            foreach (var slotVM in vm.displaySlots)
            {
                CreateSlot(slotVM);
            }
        }).AddTo(this);

        foreach (var slotVM in vm.displaySlots)
        {
            CreateSlot(slotVM);
        }
        
    }

    void CreateSlot(ItemSlotViewModel slotVM)
    {
        var slotView = Instantiate(slotPrefab, slotParent);
        slotView.Bind(slotVM);
        slotVM.onClick.Subscribe(_ =>
        {
            middleVM.SelectItem(slotVM);
        }).AddTo(this);
        activeSlots.Add(slotView);
    }
}
