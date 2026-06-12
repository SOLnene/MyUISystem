using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

/// <summary>
/// 抽卡流程控制 一次抽卡行为的生命周期
/// </summary>
public class GachaSessionViewModel: IDisposable
{
    public ReactiveCollection<GachaEntryViewModel> Items { get; }
    public ReactiveProperty<int> CurrentIndex { get; } = new ReactiveProperty<int>(0);
    
    public IReadOnlyReactiveProperty<GachaEntryViewModel> CurrentItem { get; }
    public IReadOnlyReactiveProperty<bool> HasNext { get; }

    public ReactiveProperty<GachaSessionPhase> Phase { get; }
        = new ReactiveProperty<GachaSessionPhase>(GachaSessionPhase.Revealing);
    
    //不带数据的事件流
    public Subject<Unit> OnPreviewFinished { get; } = new Subject<Unit>();
    public Subject<Unit> OnSessionFinished { get; } = new Subject<Unit>();
  
    
    CompositeDisposable disposable = new CompositeDisposable();
    public GachaSessionViewModel(IReadOnlyList<GachaEntryViewModel> result)
    {
        Items = result.ToReactiveCollection();

        CurrentItem = CurrentIndex.Select(i =>
                i >= 0 && i < Items.Count
                    ? Items[i]
                    : null)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        HasNext = CurrentIndex
            .Select(i => i < Items.Count - 1)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
    }

    public void Next()
    {
        if (HasNext.Value)
        {
            CurrentIndex.Value++;
        }
        else
        {
            EnterPreview();
        }
    }

    public void SkipReveal()
    {
        EnterPreview();
    }

    void EnterPreview()
    {
        if (Phase.Value != GachaSessionPhase.Revealing)
        {
            return;
        }

        Phase.Value = GachaSessionPhase.Preview;
        OnPreviewFinished.OnNext(Unit.Default);
    }

    public void FinishSession()
    {
        Phase.Value = GachaSessionPhase.Finished;
        OnSessionFinished.OnNext(Unit.Default);
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}

/// <summary>
/// 抽卡流程阶段
/// </summary>
public enum GachaSessionPhase
{
    Revealing,  //逐个展示
    Preview,    //汇总展示
    Finished
}
