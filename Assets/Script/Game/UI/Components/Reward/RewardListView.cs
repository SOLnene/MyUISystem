using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class RewardListView : MonoBehaviour
{
    [SerializeField]
    RectTransform content;
    [SerializeField]
    RewardItemView itemPrefab;
    [SerializeField]
    AnimatedPanelGroup itemPanelGroup;

    readonly List<RewardItemView> itemViews = new();
    readonly List<ItemSlotViewModel> itemSlotViewModels = new();
    readonly List<AnimatedPanel> visibleItemPanels = new();

    void Awake()
    {
        content.GetComponentsInChildren(true, itemViews);
        for (int i = 0; i < itemViews.Count; i++)
        {
            itemSlotViewModels.Add(new ItemSlotViewModel());
        }
    }

    public void Bind(
        IReadOnlyList<RewardItemData> rewards,
        ItemDatabase itemDatabase)
    {
        Clear();
        if (rewards == null || itemDatabase == null)
        {
            return;
        }

        int visibleItemCount = 0;
        foreach (RewardItemData reward in rewards)
        {
            if (reward.Count <= 0)
            {
                continue;
            }

            ItemDefinition itemDefinition =
                itemDatabase.GetItemByID(reward.ItemId);
            if (itemDefinition == null)
            {
                Debug.LogWarning(
                    $"Reward item definition is missing: itemId={reward.ItemId}");
                continue;
            }

            if (visibleItemCount == itemViews.Count)
            {
                itemViews.Add(Instantiate(itemPrefab, content));
                itemSlotViewModels.Add(new ItemSlotViewModel());
            }

            ItemSlotViewModel itemSlotViewModel =
                itemSlotViewModels[visibleItemCount];
            itemSlotViewModel.isEmpty.Value = false;
            itemSlotViewModel.iconPath.Value = itemDefinition.iconPath;
            itemSlotViewModel.count.Value = reward.Count.ToString();
            itemSlotViewModel.color.Value =
                RarityConfig.GetColor(itemDefinition.itemRarity);
            itemSlotViewModel.star.Value = itemDefinition.stars;

            RewardItemView itemView = itemViews[visibleItemCount];
            itemView.gameObject.SetActive(true);
            itemView.Bind(itemDefinition, itemSlotViewModel);
            visibleItemPanels.Add(itemView.AnimationPanel);
            visibleItemCount++;
        }

        itemPanelGroup.Show(visibleItemPanels).Forget();
    }

    public void Clear()
    {
        itemPanelGroup.HideAllImmediate();
        visibleItemPanels.Clear();

        foreach (RewardItemView itemView in itemViews)
        {
            itemView.Clear();
            itemView.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        foreach (ItemSlotViewModel itemSlotViewModel in itemSlotViewModels)
        {
            itemSlotViewModel.Dispose();
        }
    }
}
