using System;
using System.Collections.Generic;
using UniRx;

public sealed class AchievementCategoryTabViewModel : IDisposable
{
    // 分类 VM 持有并释放本分类的成就 VM，页面 VM 不重复管理这份生命周期。
    readonly List<AchievementItemViewModel> items;
    readonly CompositeDisposable disposable = new();
    readonly ReactiveProperty<int> progressPercent = new();
    readonly Subject<Unit> itemsChanged = new();

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<AchievementItemViewModel> Items => items;
    public IReadOnlyReactiveProperty<int> ProgressPercent => progressPercent;
    public IReadOnlyReactiveProperty<bool> HasClaimableReward { get; }
    internal IObservable<Unit> ItemsChanged => itemsChanged;

    internal AchievementCategoryTabViewModel(
        AchievementCategoryState state,
        List<AchievementItemViewModel> items)
    {
        Id = state.Id;
        Name = state.Name;
        HasClaimableReward = state.HasClaimableReward;
        this.items = items;

        // 完成状态变化刷新可领取排序，领取状态变化刷新分类进度并通知页面重新排序。
        foreach (AchievementItemViewModel item in items)
        {
            item.IsCompleted
                .Skip(1)
                .Subscribe(_ =>
                {
                    RefreshProgressPercent();
                    itemsChanged.OnNext(Unit.Default);
                })
                .AddTo(disposable);
            item.IsClaimed
                .Skip(1)
                .Subscribe(_ => RefreshState())
                .AddTo(disposable);
        }

        RefreshProgressPercent();
    }

    public void Dispose()
    {
        disposable.Dispose();
        // 分类销毁时一并释放其成就项订阅和奖励槽状态。
        foreach (AchievementItemViewModel item in items)
        {
            item.Dispose();
        }

        progressPercent.Dispose();
        itemsChanged.Dispose();
    }

    void RefreshState()
    {
        RefreshProgressPercent();
        itemsChanged.OnNext(Unit.Default);
    }

    void RefreshProgressPercent()
    {
        // 分类进度表示“已领取成就数 / 分类总数”，完成但未领取的成就不计入。
        int claimedCount = 0;
        foreach (AchievementItemViewModel item in items)
        {
            if (item.IsClaimed.Value)
            {
                claimedCount++;
            }
        }

        progressPercent.Value = items.Count == 0 ? 0 : claimedCount * 100 / items.Count;
    }
}
