using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class StoreItemListView : MonoBehaviour
{
    [SerializeField]
    Transform contentRoot;
    [SerializeField]
    StoreItemView itemPrefab;
    [SerializeField, FormerlySerializedAs("fakeCostIcon")]
    Sprite costIcon;
    [SerializeField]
    Color bottomColor = new Color32(94, 111, 132, 255);
    [SerializeField]
    Color frameColor = new Color32(225, 232, 236, 255);

    readonly List<StoreItemView> itemViews = new();

    public Sprite CostIcon => costIcon;
    public Color BottomColor => bottomColor;
    public Color FrameColor => frameColor;

    public void Bind(IReadOnlyList<StoreItemViewData> items)
    {
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

    void OnItemClicked(StoreItemViewData itemData)
    {
        Debug.Log($"Clicked store item: {itemData.Id}");
    }
}
