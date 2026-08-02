using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

public sealed class AchievementViewModel : IDisposable
{
    const string ConfigAddress = "config/achievement";

    readonly ItemDatabase itemDatabase;
    // 分类 VM 独占分类内的成就 VM，页面 VM 只负责分类选择和跨分类统计。
    readonly List<AchievementCategoryTabViewModel> categories = new();
    // 这是当前分类的投影列表，不重复持有成就 VM 的所有权。
    readonly List<AchievementItemViewModel> visibleItems = new();
    readonly VersionedAssetLoader<TextAsset> configLoader = new();
    readonly CompositeDisposable categoryStateSubscriptions = new();
    readonly CompositeDisposable disposable = new();
    readonly ReactiveProperty<string> selectedCategoryId = new();
    readonly ReactiveProperty<AchievementCountInfo> countInfo = new();
    readonly Subject<Unit> visibleItemsChanged = new();

    public IReadOnlyList<AchievementCategoryTabViewModel> Categories => categories;
    public IReadOnlyList<AchievementItemViewModel> VisibleItems => visibleItems;
    public IReadOnlyReactiveProperty<string> SelectedCategoryId => selectedCategoryId;
    public IReadOnlyReactiveProperty<AchievementCountInfo> CountInfo => countInfo;
    internal IObservable<Unit> VisibleItemsChanged => visibleItemsChanged;

    public AchievementViewModel(ItemDatabase itemDatabase)
    {
        this.itemDatabase = itemDatabase;
        selectedCategoryId
            .Subscribe(_ => RefreshVisibleItems())
            .AddTo(disposable);
    }

    public async UniTask LoadAsync(CancellationToken cancellationToken)
    {
        VersionedAssetLoadResult<TextAsset> result =
            await configLoader.LoadAsync(ConfigAddress, cancellationToken);
        if (!result.IsCurrent)
        {
            return;
        }

        AchievementConfigData config;
        try
        {
            config = JsonConvert.DeserializeObject<AchievementConfigData>(result.Asset.text);
        }
        catch (JsonException exception)
        {
            Debug.LogError($"Achievement config parse failed: {exception.Message}");
            return;
        }

        if (!AchievementConfigValidator.TryValidate(config, itemDatabase))
        {
            return;
        }

        ClearCategories();

        // 配表顺序只决定左侧分类顺序，右侧成就顺序由完成状态统一计算。
        foreach (AchievementCategoryConfigData category in config.categories.OrderBy(category => category.order))
        {
            List<AchievementItemViewModel> categoryItems = new();
            foreach (AchievementDefinition definition in category.achievements)
            {
                ItemDefinition rewardItem =
                    itemDatabase.GetItemByID(definition.reward.itemId);
                categoryItems.Add(new AchievementItemViewModel(
                    definition,
                    rewardItem,
                    definition.reward.amount));
            }

            // 分类 VM 接管 categoryItems 的生命周期，页面 VM 不再单独 Dispose 成就项。
            AchievementCategoryTabViewModel categoryViewModel = new(
                category.id,
                category.name,
                categoryItems);
            categories.Add(categoryViewModel);
            categoryViewModel.ItemsChanged
                .Subscribe(_ => RefreshCategoryState(categoryViewModel))
                .AddTo(categoryStateSubscriptions);
        }

        if (categories.Count > 0)
        {
            selectedCategoryId.Value = categories[0].Id;
        }

        RefreshVisibleItems();
        RefreshCountInfo();
    }

    public void SelectCategory(string categoryId)
    {
        // 使用稳定的分类 ID 而不是数组下标，避免配表排序变化导致选中项错位。
        if (categories.Any(category => category.Id == categoryId))
        {
            selectedCategoryId.Value = categoryId;
        }
    }

    public void Dispose()
    {
        configLoader.Dispose();
        ClearCategories();
        categoryStateSubscriptions.Dispose();
        disposable.Dispose();
        selectedCategoryId.Dispose();
        countInfo.Dispose();
        visibleItemsChanged.Dispose();
    }

    void ClearCategories()
    {
        // 清理旧分类前先解除订阅，避免旧 VM 在新页面加载期间继续刷新列表。
        categoryStateSubscriptions.Clear();
        selectedCategoryId.Value = null;
        foreach (AchievementCategoryTabViewModel category in categories)
        {
            category.Dispose();
        }

        categories.Clear();
        visibleItems.Clear();
        RefreshCountInfo();
    }

    void RefreshCategoryState(AchievementCategoryTabViewModel category)
    {
        // 顶部统计跨所有分类；右侧列表只在变化来自当前分类时重排。
        RefreshCountInfo();
        if (selectedCategoryId.Value == category.Id)
        {
            RefreshVisibleItems();
        }
    }

    void RefreshVisibleItems()
    {
        // 右侧只绑定当前分类，并把可领取项排在未完成项之前，已领取项最后。
        visibleItems.Clear();
        AchievementCategoryTabViewModel selectedCategory = categories
            .FirstOrDefault(category => category.Id == selectedCategoryId.Value);
        if (selectedCategory != null)
        {
            visibleItems.AddRange(selectedCategory.Items.OrderBy(GetDisplayPriority));
        }

        visibleItemsChanged.OnNext(Unit.Default);
    }

    static int GetDisplayPriority(AchievementItemViewModel item)
    {
        // 数值越小越靠前：未领取的完成项、未完成项、已领取项。
        if (item.IsClaimed.Value)
        {
            return 2;
        }

        return item.IsCompleted.Value ? 0 : 1;
    }

    void RefreshCountInfo()
    {
        int claimedCount = 0;
        int totalCount = 0;
        foreach (AchievementCategoryTabViewModel category in categories)
        {
            totalCount += category.Items.Count;
            foreach (AchievementItemViewModel item in category.Items)
            {
                if (item.IsClaimed.Value)
                {
                    claimedCount++;
                }
            }
        }

        countInfo.Value = new AchievementCountInfo(claimedCount, totalCount);
    }
}
