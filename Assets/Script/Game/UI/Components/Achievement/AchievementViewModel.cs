using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public sealed class AchievementViewModel : IDisposable
{
    const string ConfigAddress = "config/achievement";

    readonly ItemDatabase itemDatabase;
    readonly List<AchievementItemViewModel> items = new();
    readonly VersionedAssetLoader<TextAsset> configLoader = new();

    public IReadOnlyList<AchievementItemViewModel> Items => items;

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
    }

    public void Dispose()
    {
        configLoader.Dispose();
        ClearItems();
    }

    void ClearItems()
    {
        foreach (AchievementItemViewModel item in items)
        {
            item.Dispose();
        }

        items.Clear();
    }
}
