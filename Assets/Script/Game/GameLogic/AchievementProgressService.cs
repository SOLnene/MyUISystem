using System;
using System.Collections.Generic;
using UniRx;

internal readonly struct AchievementProgressChange
{
    internal string ProgressKey { get; }
    internal int Progress { get; }

    internal AchievementProgressChange(string progressKey, int progress)
    {
        ProgressKey = progressKey;
        Progress = progress;
    }
}

internal static class AchievementProgressKeys
{
    internal const string GachaDraw = "gacha_draw";
    internal const string WeaponEnhance = "weapon_enhance";
    internal const string CharacterEnhance = "character_enhance";
    internal const string StorePurchase = "store_purchase";
    internal const string AchievementViewOpen = "achievement_view_open";
}

internal sealed class AchievementProgressService
{
    internal static AchievementProgressService Instance { get; } = new();

    readonly Dictionary<string, int> progressValues =
        new(StringComparer.Ordinal);
    readonly HashSet<string> claimedAchievementIds =
        new(StringComparer.Ordinal);
    readonly Subject<AchievementProgressChange> progressChanged = new();
    readonly Subject<string> claimedChanged = new();

    internal int GetProgress(string progressKey)
    {
        if (string.IsNullOrWhiteSpace(progressKey))
        {
            return 0;
        }

        return progressValues.TryGetValue(progressKey, out int progress)
            ? progress
            : 0;
    }

    internal IObservable<int> ObserveProgress(string progressKey)
    {
        return progressChanged
            .Where(change => string.Equals(
                change.ProgressKey,
                progressKey,
                StringComparison.Ordinal))
            .Select(change => change.Progress)
            .StartWith(GetProgress(progressKey));
    }

    internal void AddProgress(string progressKey, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(progressKey) || amount <= 0)
        {
            return;
        }

        progressValues.TryGetValue(progressKey, out int currentProgress);
        int progress = currentProgress + amount;
        progressValues[progressKey] = progress;
        progressChanged.OnNext(new AchievementProgressChange(progressKey, progress));
    }

    internal IObservable<bool> ObserveClaimed(string achievementId)
    {
        return claimedChanged
            .Where(claimedId => string.Equals(
                claimedId,
                achievementId,
                StringComparison.Ordinal))
            .Select(_ => true)
            .StartWith(claimedAchievementIds.Contains(achievementId));
    }

    internal bool TryClaim(string achievementId, Func<bool> grantReward)
    {
        if (string.IsNullOrWhiteSpace(achievementId) ||
            grantReward == null ||
            claimedAchievementIds.Contains(achievementId) ||
            !grantReward())
        {
            return false;
        }

        claimedAchievementIds.Add(achievementId);
        claimedChanged.OnNext(achievementId);
        return true;
    }
}
