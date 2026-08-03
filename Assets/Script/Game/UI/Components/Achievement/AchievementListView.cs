using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class AchievementListView : MonoBehaviour
{
    const string ItemPrefabAddress = "ui/achievement/item";

    [SerializeField]
    RectTransform content;
    [SerializeField]
    AnimatedPanelGroup anim;

    readonly List<AchievementItemView> itemViews = new();
    readonly List<AnimatedPanel> activePanels = new();
    readonly VersionedAssetLoader<GameObject> itemPrefabLoader = new();

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
        anim.HideAllImmediate();
        itemPrefabLoader.Cancel();
        // 只隐藏池内对象，避免反复创建右侧成就项和重复加载 Addressable。
        foreach (AchievementItemView itemView in itemViews)
        {
            itemView.Unbind();
            itemView.gameObject.SetActive(false);
        }
    }

    internal UniTask ShowItems()
    {
        activePanels.Clear();
        foreach (var itemView in itemViews)
        {
            if (itemView.gameObject.activeSelf)
            {
                activePanels.Add(itemView.Anim);
            }
        }

        return anim.Show(activePanels);
    }

    public async UniTask<bool> PrepareAsync(
        int capacity,
        CancellationToken cancellationToken)
    {
        // 页面打开时按最大分类容量扩容，之后切换分类只做同步绑定。
        if (itemViews.Count >= capacity)
        {
            return true;
        }

        // Addressable 提供可实例化原型，具体实例由当前列表持有并复用。
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

        while (itemViews.Count < capacity)
        {
            AchievementItemView itemView = Instantiate(itemPrefab, content);
            anim.HideImmediate(itemView.Anim);
            itemViews.Add(itemView);
        }

        return true;
    }

    void OnDestroy()
    {
        itemPrefabLoader.Dispose();
    }
}
