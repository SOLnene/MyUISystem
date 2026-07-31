using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class AchievementListView : MonoBehaviour
{
    const string ItemPrefabAddress = "ui/achievement/item";

    [SerializeField]
    RectTransform content;

    readonly List<AchievementItemView> itemViews = new();
    readonly VersionedAssetLoader<GameObject> itemPrefabLoader = new();

    public async UniTask BindAsync(
        IReadOnlyList<AchievementItemViewModel> items,
        CancellationToken cancellationToken)
    {
        if (!await EnsureItemCountAsync(items.Count, cancellationToken))
        {
            return;
        }

        Refresh(items);
    }

    internal void Refresh(IReadOnlyList<AchievementItemViewModel> items)
    {
        for (int i = 0; i < itemViews.Count; i++)
        {
            bool active = i < items.Count;
            AchievementItemView itemView = itemViews[i];
            itemView.gameObject.SetActive(active);

            if (active)
            {
                itemView.Bind(items[i]);
            }
            else
            {
                itemView.Unbind();
            }
        }
    }

    public void Clear()
    {
        itemPrefabLoader.Cancel();
        foreach (AchievementItemView itemView in itemViews)
        {
            itemView.Unbind();
            itemView.gameObject.SetActive(false);
        }
    }

    async UniTask<bool> EnsureItemCountAsync(
        int count,
        CancellationToken cancellationToken)
    {
        if (itemViews.Count >= count)
        {
            return true;
        }

        VersionedAssetLoadResult<GameObject> result =
            await itemPrefabLoader.LoadAsync(ItemPrefabAddress, cancellationToken);
        if (!result.IsCurrent)
        {
            return false;
        }

        if (!result.Asset.TryGetComponent(out AchievementItemView itemPrefab))
        {
            Debug.LogError(
                $"Achievement item prefab does not contain {nameof(AchievementItemView)}: {ItemPrefabAddress}");
            return false;
        }

        while (itemViews.Count < count)
        {
            itemViews.Add(Instantiate(itemPrefab, content));
        }

        return true;
    }

    void OnDestroy()
    {
        itemPrefabLoader.Dispose();
    }
}
