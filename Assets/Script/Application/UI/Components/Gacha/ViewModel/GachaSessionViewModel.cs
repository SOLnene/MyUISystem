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
    public ReactiveCollection<GachaEntryViewModel> items { get; }
    public ReactiveProperty<int> currentIndex { get; } = new ReactiveProperty<int>(0);
    
    public IReadOnlyReactiveProperty<GachaEntryViewModel> CurrentItem { get; }
    public IReadOnlyReactiveProperty<bool> HasNext { get; }
    
    //不带数据的事件流
    public Subject<Unit> OnPreviewFinished { get; } = new Subject<Unit>();
    public Subject<Unit> OnSessionFinished { get; } = new Subject<Unit>();
  
    
    CompositeDisposable disposable = new CompositeDisposable();
    public GachaSessionViewModel(IReadOnlyList<GachaEntryViewModel> result)
    {
        items = result.ToReactiveCollection();

        CurrentItem = currentIndex.Select(i =>
                i >= 0 && i < items.Count
                    ? items[i]
                    : null)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        HasNext = currentIndex
            .Select(_ => currentIndex.Value < items.Count - 1)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
    }
    
    public void Next()
    {
        if ( currentIndex.Value < items.Count - 1)
            currentIndex.Value++;
    }

    public void Skip()
    {
        currentIndex.Value = items.Count - 1;
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
