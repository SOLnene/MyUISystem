using System.Collections.Generic;
using UnityEngine;

public class StoreItemListView : MonoBehaviour
{
    [SerializeField]
    Transform contentRoot;
    [SerializeField]
    StoreItemView itemPrefab;
    [SerializeField]
    Sprite fakeItemIcon;
    [SerializeField]
    Sprite fakeCostIcon;
    [SerializeField]
    Color blueBgColor = new Color32(122, 178, 202, 255);
    [SerializeField]
    Color purpleBgColor = new Color32(172, 124, 202, 255);
    [SerializeField]
    Color orangeBgColor = new Color32(210, 154, 74, 255);
    [SerializeField]
    Color bottomColor = new Color32(94, 111, 132, 255);
    [SerializeField]
    Color frameColor = new Color32(225, 232, 236, 255);

    readonly List<StoreItemView> itemViews = new();

    public void LoadFakeItems()
    {
        Bind(CreateFakeItems());
    }

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

    List<StoreItemViewData> CreateFakeItems()
    {
        return new List<StoreItemViewData>
        {
            new StoreItemViewData(
                "100001",
                "冒险家的经验x3",
                fakeItemIcon,
                fakeCostIcon,
                8,
                blueBgColor,
                bottomColor,
                frameColor,
                remainCount: 100,
                hasRemainCount: true),
            new StoreItemViewData(
                "100002",
                "纠缠之缘",
                fakeItemIcon,
                fakeCostIcon,
                75,
                purpleBgColor,
                bottomColor,
                frameColor,
                beforeValue: 125,
                remainCount: 5,
                discountPercent: 40,
                hasBeforeValue: true,
                hasRemainCount: true,
                hasDiscount: true),
            new StoreItemViewData(
                "100003",
                "相遇之缘",
                fakeItemIcon,
                fakeCostIcon,
                5,
                orangeBgColor,
                bottomColor,
                frameColor,
                remainCount: 10,
                hasRemainCount: true)
        };
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
