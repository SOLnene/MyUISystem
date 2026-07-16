using System;
using System.Collections.Generic;
using UnityEngine;

public class StoreItemListView : MonoBehaviour
{
    [SerializeField]
    Transform contentRoot;
    [SerializeField]
    StoreItemView itemPrefab;

    readonly List<StoreItemView> itemViews = new();
    Action<StoreItemViewModel> onItemClicked;

    public void Bind(IReadOnlyList<StoreItemViewModel> items, Action<StoreItemViewModel> clickHandler)
    {
        onItemClicked = clickHandler;
        EnsureItemCount(items.Count);

        for (int i = 0; i < itemViews.Count; i++)
        {
            bool active = i < items.Count;
            itemViews[i].gameObject.SetActive(active);

            if (active)
            {
                itemViews[i].Bind(items[i], OnItemClicked);
            }
        }
    }

    void EnsureItemCount(int count)
    {
        while (itemViews.Count < count)
        {
            StoreItemView itemView = Instantiate(itemPrefab, contentRoot);
            itemViews.Add(itemView);
        }
    }

    void OnItemClicked(StoreItemViewModel itemViewModel)
    {
        onItemClicked?.Invoke(itemViewModel);
    }
}
