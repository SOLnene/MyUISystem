using System.Collections.Generic;
using UnityEngine;

public class RewardListView : MonoBehaviour
{
    [SerializeField]
    RectTransform content;
    [SerializeField]
    RewardItemView itemPrefab;

    readonly List<RewardItemView> itemViews = new();
    bool initialized;

    public void Bind(IReadOnlyList<RewardItemDisplayData> items)
    {
        Initialize();

        int itemCount = items?.Count ?? 0;
        EnsureCapacity(itemCount);

        for (int i = 0; i < itemViews.Count; i++)
        {
            bool shouldShow = i < itemCount;
            itemViews[i].gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                itemViews[i].Bind(items[i]);
            }
            else
            {
                itemViews[i].Clear();
            }
        }
    }

    public void Clear()
    {
        Initialize();

        foreach (RewardItemView itemView in itemViews)
        {
            itemView.Clear();
            itemView.gameObject.SetActive(false);
        }
    }

    void Initialize()
    {
        if (initialized)
        {
            return;
        }

        content.GetComponentsInChildren(true, itemViews);
        initialized = true;
    }

    void EnsureCapacity(int itemCount)
    {
        while (itemViews.Count < itemCount)
        {
            RewardItemView itemView = Instantiate(itemPrefab, content);
            itemViews.Add(itemView);
        }
    }
}
