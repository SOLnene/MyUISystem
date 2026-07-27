using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

public sealed class AchievementViewModel : IDisposable
{
    const string ConfigAddress = "config/achievement";

    readonly ItemDatabase itemDatabase;
    readonly List<AchievementItemViewModel> items = new();
    readonly VersionedAssetLoader<TextAsset> configLoader = new();
    readonly CompositeDisposable itemStateSubscriptions = new();
    readonly ReactiveProperty<AchievementCountInfo> countInfo = new();

    public IReadOnlyList<AchievementItemViewModel> Items => items;
    public IReadOnlyReactiveProperty<AchievementCountInfo> CountInfo => countInfo;

    public AchievementViewModel(ItemDatabase itemDatabase)
    {
        this.itemDatabase = itemDatabase;
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

        ClearItems();
        if (config?.categories == null)
        {
            return;
        }

        foreach (AchievementCategoryConfigData category in config.categories)
        {
            if (category?.achievements == null)
            {
                continue;
            }

            foreach (AchievementDefinition definition in category.achievements)
            {
                if (definition?.reward == null)
                {
                    Debug.LogWarning($"Achievement reward is missing: {definition?.id}");
                    continue;
                }

                ItemDefinition rewardItem =
                    itemDatabase.GetItemByID(definition.reward.itemId);
                if (rewardItem == null)
                {
                    Debug.LogWarning(
                        $"Achievement reward item is missing: {definition.id}, itemId={definition.reward.itemId}");
                    continue;
                }

                items.Add(new AchievementItemViewModel(
                    definition,
                    rewardItem,
                    definition.reward.amount));
            }
        }

        BindItemState();
        RefreshCountInfo();
    }

    public void Dispose()
    {
        configLoader.Dispose();
        ClearItems();
        itemStateSubscriptions.Dispose();
        countInfo.Dispose();
    }

    void ClearItems()
    {
        itemStateSubscriptions.Clear();
        foreach (AchievementItemViewModel item in items)
        {
            item.Dispose();
        }

        items.Clear();
        RefreshCountInfo();
    }

    void BindItemState()
    {
        foreach (AchievementItemViewModel item in items)
        {
            item.IsCompleted
                .Skip(1)
                .Subscribe(_ => RefreshCountInfo())
                .AddTo(itemStateSubscriptions);
        }
    }

    void RefreshCountInfo()
    {
        int completedCount = 0;
        foreach (AchievementItemViewModel item in items)
        {
            if (item.IsCompleted.Value)
            {
                completedCount++;
            }
        }

        countInfo.Value = new AchievementCountInfo(completedCount, items.Count);
    }
}
