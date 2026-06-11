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
    [SerializeField]
    ItemSlotView slotPrefab;
    [SerializeField]
    float slotShowDelay = 0.03f;

    BackpackMiddleViewModel middleVM;

    readonly List<ItemSlotView> activeSlots = new();
    readonly CompositeDisposable bindDisposables = new();
    readonly CompositeDisposable slotDisposables = new();
    CancellationTokenSource slotAnimationCts;
    PrefabPool slotPool;

    void Awake()
    {
        slotPool = PrefabPool.Create(slotPrefab.gameObject);
    }

    public void Bind(BackpackMiddleViewModel vm)
    {
        bindDisposables.Clear();
        middleVM = vm;
        ResetDisplayedSlots();

        vm.displaySlots.ObserveAdd().Subscribe(add =>
        {
            CreateSlot(add.Value, activeSlots.Count, slotAnimationCts.Token);
        }).AddTo(bindDisposables);

        vm.displaySlots.ObserveRemove().Subscribe(rem =>
        {
            var view = activeSlots.Find(v => v.vm == rem.Value);
            if (view != null)
            {
                RecycleSlot(view);
                activeSlots.Remove(view);
            }
        }).AddTo(bindDisposables);

        vm.displaySlots.ObserveReset().Subscribe(_ =>
        {
            ResetDisplayedSlots();
            foreach (var slotVM in vm.displaySlots)
            {
                CreateSlot(slotVM, activeSlots.Count, slotAnimationCts.Token);
            }
        }).AddTo(bindDisposables);

        foreach (var slotVM in vm.displaySlots)
        {
            CreateSlot(slotVM, activeSlots.Count, slotAnimationCts.Token);
        }
    }

    void CreateSlot(ItemSlotViewModel slotVM, int showIndex, CancellationToken cancellationToken)
    {
        var slotView = slotPool.Get(slotParent).GetComponent<ItemSlotView>();
        slotView.Bind(slotVM);
        slotView.HideImmediate();
        slotVM.onClick.Subscribe(_ =>
        {
            middleVM.SelectItem(slotVM);
        }).AddTo(slotDisposables);
        activeSlots.Add(slotView);
        ShowSlotAsync(slotView, showIndex, cancellationToken).Forget();
    }

    async UniTaskVoid ShowSlotAsync(ItemSlotView slotView, int showIndex, CancellationToken cancellationToken)
    {
        try
        {
            if (showIndex > 0)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(showIndex * slotShowDelay),
                    cancellationToken: cancellationToken);
            }

            await slotView.Show().AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    void ResetDisplayedSlots()
    {
        slotAnimationCts?.Cancel();
        slotAnimationCts?.Dispose();
        slotAnimationCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        ClearSlots();
    }

    void ClearSlots()
    {
        slotDisposables.Clear();
        foreach (var slotView in activeSlots)
        {
            if (slotView != null)
            {
                RecycleSlot(slotView);
            }
        }
        activeSlots.Clear();
    }

    void RecycleSlot(ItemSlotView slotView)
    {
        slotView.ResetState();
        slotPool.Recycle(slotView.gameObject);
    }

    void OnDestroy()
    {
        slotAnimationCts?.Cancel();
        slotAnimationCts?.Dispose();
        ClearSlots();
        slotPool?.Destroy();
        slotDisposables.Dispose();
        bindDisposables.Dispose();
    }
}
