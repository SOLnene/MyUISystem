using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public class BackpackItemGridView : MonoBehaviour
{
    [SerializeField]
    Transform slotParent;
    
    string slotPrefabAddress = "ui/prefab/item_slot_backpack";

    BackpackMiddleViewModel middleVM;

    readonly List<ItemSlotView> activeSlots = new();
    readonly CompositeDisposable bindDisposables = new();
    CancellationTokenSource bindCts;

    public void Bind(BackpackMiddleViewModel vm)
    {
        bindCts?.Cancel();
        bindCts?.Dispose();
        bindCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        bindDisposables.Clear();
        ClearSlots();
        middleVM = vm;

        vm.displaySlots.ObserveAdd().Subscribe(add =>
        {
            CreateSlotAsync(add.Value, bindCts.Token).Forget();
        }).AddTo(bindDisposables);

        vm.displaySlots.ObserveRemove().Subscribe(rem =>
        {
            var view = activeSlots.Find(v => v.vm == rem.Value);
            if (view != null)
            {
                ResourceManager.Instance.Recycle(view.gameObject);
                activeSlots.Remove(view);
            }
        }).AddTo(bindDisposables);

        vm.displaySlots.ObserveReset().Subscribe(_ =>
        {
            ClearSlots();
            foreach (var slotVM in vm.displaySlots)
            {
                CreateSlotAsync(slotVM, bindCts.Token).Forget();
            }
        }).AddTo(bindDisposables);

        foreach (var slotVM in vm.displaySlots)
        {
            CreateSlotAsync(slotVM, bindCts.Token).Forget();
        }
    }

    async UniTaskVoid CreateSlotAsync(ItemSlotViewModel slotVM, CancellationToken cancellationToken)
    {
        ItemSlotView slotView;
        try
        {
            slotView = await ItemFactory.InstantiateItemSlot(slotVM, slotParent, slotPrefabAddress)
                .AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (slotView == null)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || middleVM == null || !middleVM.displaySlots.Contains(slotVM))
        {
            ResourceManager.Instance.Recycle(slotView.gameObject);
            return;
        }

        slotView.Bind(slotVM);
        slotVM.onClick.Subscribe(_ =>
        {
            middleVM.SelectItem(slotVM);
        }).AddTo(bindDisposables);
        activeSlots.Add(slotView);
    }

    void ClearSlots()
    {
        foreach (var slotView in activeSlots)
        {
            if (slotView != null)
            {
                ResourceManager.Instance.Recycle(slotView.gameObject);
            }
        }
        activeSlots.Clear();
    }

    void OnDestroy()
    {
        bindCts?.Cancel();
        bindCts?.Dispose();
        bindDisposables.Dispose();
    }
}
