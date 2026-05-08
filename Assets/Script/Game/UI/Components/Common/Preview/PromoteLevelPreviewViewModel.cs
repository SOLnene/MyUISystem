using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class PromoteLevelPreviewViewModel : IDisposable
{
    public readonly IPromotable model;

    public ReadOnlyReactiveProperty<int> rank;
    public ReadOnlyReactiveProperty<bool> maxRanked;

    public ReadOnlyReactiveProperty<string> currentLevelText;
    public ReadOnlyReactiveProperty<string> nextLevelText;

    public ReadOnlyReactiveProperty<int> currentStarCount;
    public ReadOnlyReactiveProperty<int> nextStarCount;

    readonly CompositeDisposable disposable = new CompositeDisposable();

    public PromoteLevelPreviewViewModel(IPromotable model)
    {
        this.model = model;

        rank = model.RankRP
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        maxRanked = model.RankRP
            .Select(_ => model.RankSystem.IsMaxRank())
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        currentLevelText = model.RankRP
            .Select(_ => $"Lv.{model.GetCurrentMaxLevel()}")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        nextLevelText = model.RankRP
            .Select(_ =>
            {
                if (model.RankSystem.IsMaxRank())
                    return string.Empty;

                return $"Lv.{model.RankSystem.GetNextRankMaxLevel()}";
            })
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        currentStarCount = model.RankRP
            .Select(rank => rank)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        nextStarCount = model.RankRP
            .Select(rank =>
            {
                if (model.RankSystem.IsMaxRank())
                    return rank;

                return rank + 1;
            })
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
