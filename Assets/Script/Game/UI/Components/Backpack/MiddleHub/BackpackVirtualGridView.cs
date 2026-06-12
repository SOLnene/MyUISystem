using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class BackpackVirtualGridView : MonoBehaviour
{
    [SerializeField]
    ScrollRect scrollRect;
    [SerializeField]
    RectTransform viewport;
    [SerializeField]
    RectTransform content;
    [SerializeField]
    GridLayoutGroup gridLayout;
    [SerializeField]
    ItemSlotView slotPrefab;
    [SerializeField]
    int bufferRows = 1;
    [SerializeField]
    float slotShowDelay = 0.03f;

    readonly List<ItemSlotView> slots = new();
    readonly Dictionary<ItemSlotView, CompositeDisposable> slotClickSubscriptions = new();
    readonly Dictionary<ItemSlotView, CancellationTokenSource> slotAnimationSources = new();
    readonly CompositeDisposable collectionSubscriptions = new();

    BackpackMiddleViewModel middleVM;
    PrefabPool slotPool;
    RectOffset basePadding;
    int firstBoundRow;
    bool reloadScheduled;
    bool isRebuildingLayout;

    int ColumnCount => gridLayout.constraintCount;
    float RowStride => gridLayout.cellSize.y + gridLayout.spacing.y;
    int ItemCount => middleVM.displaySlots.Count;
    int TotalRows => Mathf.CeilToInt(ItemCount / (float)ColumnCount);
    int PoolRowCount => slots.Count / ColumnCount;
    int MaxFirstBoundRow => Mathf.Max(0, TotalRows - PoolRowCount);

    void Awake()
    {
        basePadding = new RectOffset(
            gridLayout.padding.left,
            gridLayout.padding.right,
            gridLayout.padding.top,
            gridLayout.padding.bottom);
        slotPool = PrefabPool.Create(slotPrefab.gameObject);
        scrollRect.onValueChanged.AddListener(OnScroll);
    }

    public void Bind(BackpackMiddleViewModel vm)
    {
        collectionSubscriptions.Clear();
        middleVM = vm;

        vm.displaySlots.ObserveReset()
            .Subscribe(_ => ScheduleReload())
            .AddTo(collectionSubscriptions);
        vm.displaySlots.ObserveAdd()
            .Subscribe(_ => ScheduleReload())
            .AddTo(collectionSubscriptions);
        vm.displaySlots.ObserveRemove()
            .Subscribe(_ => ScheduleReload())
            .AddTo(collectionSubscriptions);

        ScheduleReload();
    }

    void ScheduleReload()
    {
        if (reloadScheduled)
        {
            return;
        }

        // Clear followed by many Add events should produce one completed reload.
        reloadScheduled = true;
        ReloadNextFrame(this.GetCancellationTokenOnDestroy()).Forget();
    }

    async UniTaskVoid ReloadNextFrame(CancellationToken cancellationToken)
    {
        bool canceled = await UniTask
            .Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken)
            .SuppressCancellationThrow();
        if (canceled)
        {
            return;
        }

        reloadScheduled = false;
        ReloadFromItems(true);
    }

    void ReloadFromItems(bool playEnterAnimation)
    {
        CancelAllSlotAnimations();
        EnsureSlotPool();

        scrollRect.StopMovement();
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        firstBoundRow = 0;

        BindAllSlots(playEnterAnimation);
        UpdateVirtualPadding();
    }

    void EnsureSlotPool()
    {
        int visibleRows = Mathf.Max(1, Mathf.CeilToInt(viewport.rect.height / RowStride));
        int requiredRows = visibleRows + bufferRows * 2;
        int requiredSlots = requiredRows * ColumnCount;

        while (slots.Count < requiredSlots)
        {
            var slot = slotPool.Get(content).GetComponent<ItemSlotView>();
            slot.HideImmediate();
            slot.gameObject.SetActive(false);
            slots.Add(slot);
            slotClickSubscriptions.Add(slot, new CompositeDisposable());
        }
    }

    void OnScroll(Vector2 _)
    {
        if (isRebuildingLayout || middleVM == null || TotalRows <= PoolRowCount)
        {
            return;
        }

        MoveWindowToRow(CalculateTargetFirstBoundRow());
    }

    int CalculateTargetFirstBoundRow()
    {
        float scrollY = Mathf.Max(0f, content.anchoredPosition.y);
        int firstVisibleRow = Mathf.FloorToInt((scrollY - basePadding.top) / RowStride);
        return Mathf.Clamp(firstVisibleRow - bufferRows, 0, MaxFirstBoundRow);
    }

    void MoveWindowToRow(int targetFirstRow)
    {
        int rowDelta = targetFirstRow - firstBoundRow;
        if (rowDelta == 0)
        {
            return;
        }

        if (Mathf.Abs(rowDelta) >= PoolRowCount)
        {
            firstBoundRow = targetFirstRow;
            BindAllSlots(true);
        }
        else if (rowDelta > 0)
        {
            for (int i = 0; i < rowDelta; i++)
            {
                RecycleTopRowToBottom();
            }
        }
        else
        {
            for (int i = 0; i < -rowDelta; i++)
            {
                RecycleBottomRowToTop();
            }
        }

        UpdateVirtualPadding();
    }

    void RecycleTopRowToBottom()
    {
        var movedRow = slots.GetRange(0, ColumnCount);
        slots.RemoveRange(0, ColumnCount);
        slots.AddRange(movedRow);

        foreach (var slot in movedRow)
        {
            slot.transform.SetAsLastSibling();
        }

        firstBoundRow++;
        int firstDataIndex = (firstBoundRow + PoolRowCount - 1) * ColumnCount;
        BindSlotsStartingAt(movedRow, firstDataIndex);
    }

    void RecycleBottomRowToTop()
    {
        int lastRowStart = slots.Count - ColumnCount;
        var movedRow = slots.GetRange(lastRowStart, ColumnCount);
        slots.RemoveRange(lastRowStart, ColumnCount);
        slots.InsertRange(0, movedRow);

        for (int i = movedRow.Count - 1; i >= 0; i--)
        {
            movedRow[i].transform.SetAsFirstSibling();
        }

        firstBoundRow--;
        BindSlotsStartingAt(movedRow, firstBoundRow * ColumnCount);
    }

    void BindSlotsStartingAt(List<ItemSlotView> rowSlots, int firstDataIndex)
    {
        for (int i = 0; i < rowSlots.Count; i++)
        {
            BindSlot(rowSlots[i], firstDataIndex + i, i, true);
        }
    }

    void BindAllSlots(bool playEnterAnimation)
    {
        int firstDataIndex = firstBoundRow * ColumnCount;
        for (int i = 0; i < slots.Count; i++)
        {
            BindSlot(slots[i], firstDataIndex + i, i, playEnterAnimation);
        }
    }

    void BindSlot(ItemSlotView slot, int dataIndex, int displayOrder, bool playEnterAnimation)
    {
        CancelSlotAnimation(slot);
        var subscriptions = slotClickSubscriptions[slot];
        subscriptions.Clear();

        if (dataIndex < 0 || dataIndex >= middleVM.displaySlots.Count)
        {
            slot.ResetState();
            slot.HideImmediate();
            slot.gameObject.SetActive(false);
            return;
        }

        var slotVM = middleVM.displaySlots[dataIndex];
        slot.gameObject.SetActive(true);
        slot.Bind(slotVM);
        slotVM.onClick.Subscribe(_ => middleVM.SelectItem(slotVM)).AddTo(subscriptions);

        if (playEnterAnimation)
        {
            slot.HideImmediate();
            var animationCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            slotAnimationSources[slot] = animationCts;
            ShowSlotAsync(slot, displayOrder, animationCts).Forget();
        }
        else
        {
            slot.Show(true).Forget();
        }
    }

    async UniTaskVoid ShowSlotAsync(
        ItemSlotView slot,
        int displayOrder,
        CancellationTokenSource animationCts)
    {
        try
        {
            if (displayOrder > 0)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(displayOrder * slotShowDelay),
                    cancellationToken: animationCts.Token);
            }

            await slot.Show().AttachExternalCancellation(animationCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (slotAnimationSources.TryGetValue(slot, out var currentCts) &&
                ReferenceEquals(currentCts, animationCts))
            {
                slotAnimationSources.Remove(slot);
                animationCts.Dispose();
            }
        }
    }

    void UpdateVirtualPadding()
    {
        int remainingItems = Mathf.Max(
            0,
            ItemCount - firstBoundRow * ColumnCount);
        int representedRows = Mathf.CeilToInt(
            Mathf.Min(remainingItems, slots.Count) / (float)ColumnCount);
        int omittedBottomRows = Mathf.Max(0, TotalRows - firstBoundRow - representedRows);

        // Top and bottom padding stand in for rows without live slot objects.
        gridLayout.padding = new RectOffset(
            basePadding.left,
            basePadding.right,
            basePadding.top + Mathf.RoundToInt(firstBoundRow * RowStride),
            basePadding.bottom + Mathf.RoundToInt(omittedBottomRows * RowStride));

        isRebuildingLayout = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        isRebuildingLayout = false;
    }

    void CancelSlotAnimation(ItemSlotView slot)
    {
        if (!slotAnimationSources.TryGetValue(slot, out var animationCts))
        {
            return;
        }

        slotAnimationSources.Remove(slot);
        animationCts.Cancel();
        animationCts.Dispose();
    }

    void CancelAllSlotAnimations()
    {
        var animationSources = new List<CancellationTokenSource>(slotAnimationSources.Values);
        slotAnimationSources.Clear();

        foreach (var animationCts in animationSources)
        {
            animationCts.Cancel();
            animationCts.Dispose();
        }
    }

    void OnDestroy()
    {
        scrollRect.onValueChanged.RemoveListener(OnScroll);
        CancelAllSlotAnimations();
        collectionSubscriptions.Dispose();

        foreach (var slot in slots)
        {
            slotClickSubscriptions[slot].Dispose();
            slot.ResetState();
        }

        slotClickSubscriptions.Clear();
        slots.Clear();
        slotPool?.Destroy();
    }
}
