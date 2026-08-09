using System;
using System.Collections.Generic;
using UniRx;

internal sealed class AchievementCategoryState : IDisposable
{
    readonly List<AchievementState> achievements;
    readonly CompositeDisposable disposable = new();
    readonly ReactiveProperty<bool> hasClaimableReward = new();

    internal string Id { get; }
    internal string Name { get; }
    internal IReadOnlyList<AchievementState> Achievements => achievements;
    internal IReadOnlyReactiveProperty<bool> HasClaimableReward => hasClaimableReward;

    internal AchievementCategoryState(
        string id,
        string name,
        List<AchievementState> achievements)
    {
        Id = id;
        Name = name;
        this.achievements = achievements;

        foreach (AchievementState achievement in achievements)
        {
            achievement.CanClaim
                .Skip(1)
                .Subscribe(_ => RefreshHasClaimableReward())
                .AddTo(disposable);
        }

        RefreshHasClaimableReward();
    }

    public void Dispose()
    {
        disposable.Dispose();
        foreach (AchievementState achievement in achievements)
        {
            achievement.Dispose();
        }

        hasClaimableReward.Dispose();
    }

    void RefreshHasClaimableReward()
    {
        foreach (AchievementState achievement in achievements)
        {
            if (achievement.CanClaim.Value)
            {
                hasClaimableReward.Value = true;
                return;
            }
        }

        hasClaimableReward.Value = false;
    }
}
