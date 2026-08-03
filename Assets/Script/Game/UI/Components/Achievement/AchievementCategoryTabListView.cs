using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public sealed class AchievementCategoryTabListView : MonoBehaviour
{
    const string ItemPrefabAddress = "ui/achievement/tab";

    [SerializeField]
    RectTransform content;
    [SerializeField]
    AnimatedPanelGroup anim;

    readonly List<AchievementTabItemView> itemViews = new();
    readonly List<AnimatedPanel> activePanels = new();
    readonly VersionedAssetLoader<GameObject> itemPrefabLoader = new();
    readonly CompositeDisposable bindDisposables = new();

    public async UniTask BindAsync(
        IReadOnlyList<AchievementCategoryTabViewModel> categories,
        IReadOnlyReactiveProperty<string> selectedCategoryId,
        Action<string> onSelect,
        CancellationToken cancellationToken)
    {
        bindDisposables.Clear();
        // 第一次打开按分类数量扩容，后续打开只重新绑定已有视图。
        if (!await EnsureItemCountAsync(categories.Count, cancellationToken))
        {
            return;
        }

        for (int i = 0; i < itemViews.Count; i++)
        {
            bool active = i < categories.Count;
            AchievementTabItemView itemView = itemViews[i];
            itemView.gameObject.SetActive(active);

            if (active)
            {
                itemView.Bind(categories[i], onSelect);
            }
            else
            {
                itemView.Unbind();
            }
        }

        // 选中状态由页面 VM 的响应式 ID 驱动，Tab 自身不保存页面级选择状态。
        selectedCategoryId
            .Subscribe(RefreshSelection)
            .AddTo(bindDisposables);
    }

    public void Clear()
    {
        anim.HideAllImmediate();
        itemPrefabLoader.Cancel();
        bindDisposables.Clear();
        // 隐藏而不是销毁实例，避免每次打开成就页重复加载和实例化 prefab。
        foreach (AchievementTabItemView itemView in itemViews)
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

    async UniTask<bool> EnsureItemCountAsync(
        int count,
        CancellationToken cancellationToken)
    {
        if (itemViews.Count >= count)
        {
            return true;
        }

        // Addressable 只负责提供原型，列表实例由本 View 的池复用。
        VersionedAssetLoadResult<GameObject> result =
            await itemPrefabLoader.LoadAsync(ItemPrefabAddress, cancellationToken);
        if (!result.IsCurrent)
        {
            return false;
        }

        if (!result.Asset.TryGetComponent(out AchievementTabItemView itemPrefab))
        {
            Debug.LogError(
                $"Achievement tab item prefab does not contain {nameof(AchievementTabItemView)}: {ItemPrefabAddress}");
            return false;
        }

        while (itemViews.Count < count)
        {
            AchievementTabItemView itemView = Instantiate(itemPrefab, content);
            anim.HideImmediate(itemView.Anim);
            itemViews.Add(itemView);
        }

        return true;
    }

    void RefreshSelection(string selectedCategoryId)
    {
        // 分类 ID 改变时只更新视觉状态，不重新创建 Tab。
        foreach (AchievementTabItemView itemView in itemViews)
        {
            itemView.SetSelected(itemView.CategoryId == selectedCategoryId, true);
        }
    }

    void OnDestroy()
    {
        bindDisposables.Dispose();
        itemPrefabLoader.Dispose();
    }
}
