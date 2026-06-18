using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class UITabGroup : MonoBehaviour
{
    [SerializeField]
    List<UITabItemView> tabItems = new();

    readonly List<UITabOption> options = new();
    readonly CompositeDisposable bindDisposables = new();
    Action<int> onSelectedIndexChanged;

    public int SelectedIndex { get; private set; } = -1;
    public IReadOnlyList<UITabOption> Options => options;

    public event Action<int> OnSelectedIndexChanged;
    public event Action<UITabOption> OnSelectedOptionChanged;

    public void Bind(IReadOnlyList<UITabOption> tabOptions, int defaultIndex, Action<int> selectedHandler = null)
    {
        bindDisposables.Clear();
        onSelectedIndexChanged = selectedHandler;
        options.Clear();

        if (tabOptions != null)
        {
            options.AddRange(tabOptions);
        }

        int activeCount = Mathf.Min(options.Count, tabItems.Count);
        if (options.Count > tabItems.Count)
        {
            Debug.LogWarning($"{nameof(UITabGroup)} has fewer item views than options.");
        }

        for (int i = 0; i < tabItems.Count; i++)
        {
            bool active = i < activeCount;
            tabItems[i].gameObject.SetActive(active);

            if (active)
            {
                tabItems[i].Bind(i, options[i], Select);
            }
        }

        if (activeCount == 0)
        {
            SelectedIndex = -1;
            return;
        }

        Select(Mathf.Clamp(defaultIndex, 0, activeCount - 1), true);
    }

    public void Bind(IReadOnlyList<UITabOption> tabOptions, IReactiveProperty<int> externalSelectedIndex)
    {
        Bind(tabOptions, externalSelectedIndex.Value, index => externalSelectedIndex.Value = index);
        externalSelectedIndex
            .Subscribe(index => Select(index, true))
            .AddTo(bindDisposables);
    }

    public void Select(int index)
    {
        Select(index, false);
    }

    void Select(int index, bool silent)
    {
        if (index < 0 || index >= options.Count || index >= tabItems.Count)
        {
            Debug.LogWarning($"{nameof(UITabGroup)} selected index out of range: {index}");
            return;
        }

        if (SelectedIndex == index)
        {
            RefreshSelectedItems();
            return;
        }

        SelectedIndex = index;
        RefreshSelectedItems();

        if (silent)
        {
            return;
        }

        onSelectedIndexChanged?.Invoke(index);
        OnSelectedIndexChanged?.Invoke(index);
        OnSelectedOptionChanged?.Invoke(options[index]);
    }

    void RefreshSelectedItems()
    {
        for (int i = 0; i < tabItems.Count; i++)
        {
            tabItems[i].SetSelected(i == SelectedIndex);
        }
    }

    void OnDestroy()
    {
        bindDisposables.Dispose();
    }
}
