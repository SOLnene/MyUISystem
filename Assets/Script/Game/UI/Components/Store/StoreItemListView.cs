using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class StoreItemListView : MonoBehaviour
{
    [SerializeField]
    Transform contentRoot;
    [SerializeField]
    StoreItemView itemPrefab;
    [SerializeField]
    AnimatedPanelGroup anim;

    readonly List<StoreItemView> itemViews = new();
    readonly List<AnimatedPanel> activePanels = new();
    Action<StoreItemViewModel> onItemClicked;

    public void Bind(IReadOnlyList<StoreItemViewModel> items, Action<StoreItemViewModel> clickHandler)
    {
        onItemClicked = clickHandler;
        anim.HideAllImmediate();
        activePanels.Clear();
        EnsureItemCount(items.Count);

        for (int i = 0; i < itemViews.Count; i++)
        {
            bool active = i < items.Count;
            itemViews[i].gameObject.SetActive(active);

            if (active)
            {
                itemViews[i].Bind(items[i], OnItemClicked);
                anim.HideImmediate(itemViews[i].Anim);
                activePanels.Add(itemViews[i].Anim);
            }
        }
    }

    internal UniTask ShowItems()
    {
        return anim.Show(activePanels);
    }

    void EnsureItemCount(int count)
    {
        while (itemViews.Count < count)
        {
            StoreItemView itemView = Instantiate(itemPrefab, contentRoot);
            anim.HideImmediate(itemView.Anim);
            itemViews.Add(itemView);
        }
    }

    void OnItemClicked(StoreItemViewModel itemViewModel)
    {
        onItemClicked?.Invoke(itemViewModel);
    }
}
