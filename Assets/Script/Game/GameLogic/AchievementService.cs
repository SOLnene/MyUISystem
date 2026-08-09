using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;

internal sealed class AchievementService : IDisposable
{
    const string ConfigAddress = "config/achievement";

    readonly ItemDatabase itemDatabase;
    readonly List<AchievementCategoryState> categories = new();
    readonly Dictionary<string, AchievementState> achievementsById =
        new(StringComparer.Ordinal);
    readonly VersionedAssetLoader<TextAsset> configLoader = new();
    readonly CompositeDisposable categorySubscriptions = new();
    readonly ReactiveProperty<bool> hasClaimableReward = new();

    internal IReadOnlyList<AchievementCategoryState> Categories => categories;
    internal IReadOnlyReactiveProperty<bool> HasClaimableReward => hasClaimableReward;

    internal AchievementService(ItemDatabase itemDatabase)
    {
        this.itemDatabase = itemDatabase;
    }

    internal async UniTask InitializeAsync()
    {
        VersionedAssetLoadResult<TextAsset> result =
            await configLoader.LoadAsync(ConfigAddress, CancellationToken.None);
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

        ClearState();
        foreach (AchievementCategoryConfigData category in
                 config.categories.OrderBy(category => category.order))
        {
            List<AchievementState> categoryAchievements = new();
            foreach (AchievementDefinition definition in category.achievements)
            {
                AchievementState achievement = new(
                    definition,
                    itemDatabase.GetItemByID(definition.reward.itemId));
                categoryAchievements.Add(achievement);
                achievementsById.Add(definition.id, achievement);
            }

            AchievementCategoryState categoryState = new(
                category.id,
                category.name,
                categoryAchievements);
            categories.Add(categoryState);
            categoryState.HasClaimableReward
                .Skip(1)
                .Subscribe(_ => RefreshHasClaimableReward())
                .AddTo(categorySubscriptions);
        }

        RefreshHasClaimableReward();
    }

    internal bool TryClaim(string achievementId)
    {
        if (!achievementsById.TryGetValue(
                achievementId,
                out AchievementState achievement) ||
            !achievement.CanClaim.Value)
        {
            return false;
        }

        AchievementRewardConfigData reward = achievement.Definition.reward;
        return AchievementProgressService.Instance.TryClaim(
            achievementId,
            () => RewardGrantService.TryGrant(
                new[]
                {
                    new RewardItemData(reward.itemId, reward.amount)
                }));
    }

    public void Dispose()
    {
        ClearState();
        configLoader.Dispose();
        categorySubscriptions.Dispose();
        hasClaimableReward.Dispose();
    }

    void ClearState()
    {
        categorySubscriptions.Clear();
        foreach (AchievementCategoryState category in categories)
        {
            category.Dispose();
        }

        categories.Clear();
        achievementsById.Clear();
        hasClaimableReward.Value = false;
    }

    void RefreshHasClaimableReward()
    {
        foreach (AchievementCategoryState category in categories)
        {
            if (category.HasClaimableReward.Value)
            {
                hasClaimableReward.Value = true;
                return;
            }
        }

        hasClaimableReward.Value = false;
    }
}
