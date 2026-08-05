using System;
using System.Collections.Generic;
using UniRx;

public enum LimitedSelectionResult
{
    Added,
    Removed,
    AlreadySelected,
    NotSelected,
    LimitReached
}

public readonly struct SelectionDelta<T>
{
    public readonly T Item;
    public readonly bool Added;

    public SelectionDelta(T item, bool added)
    {
        Item = item;
        Added = added;
    }
}

public sealed class LimitedSelectionSet<T> : IDisposable
{
    readonly IEqualityComparer<T> comparer;
    readonly ReactiveCollection<T> selectedItems = new();
    readonly Subject<SelectionDelta<T>> onDelta = new();

    public IReadOnlyReactiveCollection<T> SelectedItems => selectedItems;
    public IObservable<SelectionDelta<T>> OnDelta => onDelta;
    public int MaxCount { get; }

    public LimitedSelectionSet(int maxCount, IEqualityComparer<T> comparer = null)
    {
        if (maxCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        }

        MaxCount = maxCount;
        this.comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public bool Contains(T item)
    {
        return FindSelectedIndex(item) >= 0;
    }

    public LimitedSelectionResult Toggle(T item)
    {
        return Contains(item) ? TryDeselect(item) : TrySelect(item);
    }

    public LimitedSelectionResult TrySelect(T item)
    {
        if (Contains(item))
        {
            return LimitedSelectionResult.AlreadySelected;
        }

        if (selectedItems.Count >= MaxCount)
        {
            return LimitedSelectionResult.LimitReached;
        }

        selectedItems.Add(item);
        onDelta.OnNext(new SelectionDelta<T>(item, true));
        return LimitedSelectionResult.Added;
    }

    public LimitedSelectionResult TryDeselect(T item)
    {
        int index = FindSelectedIndex(item);
        if (index < 0)
        {
            return LimitedSelectionResult.NotSelected;
        }

        selectedItems.RemoveAt(index);
        onDelta.OnNext(new SelectionDelta<T>(item, false));
        return LimitedSelectionResult.Removed;
    }

    public void Clear()
    {
        for (int index = selectedItems.Count - 1; index >= 0; index--)
        {
            TryDeselect(selectedItems[index]);
        }
    }

    public void Dispose()
    {
        Clear();
        onDelta.Dispose();
    }

    int FindSelectedIndex(T item)
    {
        for (int index = 0; index < selectedItems.Count; index++)
        {
            if (comparer.Equals(selectedItems[index], item))
            {
                return index;
            }
        }

        return -1;
    }
}
