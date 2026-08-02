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
        GameSaveCoordinator.Instance.MarkDirty();
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
        GameSaveCoordinator.Instance.MarkDirty();
        return true;
    }

    internal AchievementSaveData ExportSaveData()
    {
        AchievementSaveData saveData = new AchievementSaveData();
        foreach (KeyValuePair<string, int> pair in progressValues)
        {
            saveData.progress.Add(new AchievementProgressSaveData(pair.Key, pair.Value));
        }

        saveData.progress.Sort((left, right) =>
            string.CompareOrdinal(left.progressKey, right.progressKey));
        foreach (string achievementId in claimedAchievementIds)
        {
            saveData.claimedIds.Add(achievementId);
        }

        saveData.claimedIds.Sort(StringComparer.Ordinal);
        return saveData;
    }

    internal void ImportSaveData(AchievementSaveData saveData)
    {
        progressValues.Clear();
        claimedAchievementIds.Clear();
        if (saveData == null)
        {
            return;
        }

        if (saveData.progress != null)
        {
            foreach (AchievementProgressSaveData progressData in saveData.progress)
            {
                if (progressData == null ||
                    string.IsNullOrWhiteSpace(progressData.progressKey) ||
                    progressData.value < 0)
                {
                    continue;
                }

                progressValues[progressData.progressKey] = progressData.value;
            }
        }

        if (saveData.claimedIds == null)
        {
            return;
        }

        foreach (string achievementId in saveData.claimedIds)
        {
            if (!string.IsNullOrWhiteSpace(achievementId))
            {
                claimedAchievementIds.Add(achievementId);
            }
        }
    }
}
