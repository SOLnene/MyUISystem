using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaPoolTabViewModel : IDisposable
{
    public GachaPoolType PoolType { get;private set; }
    public ReactiveProperty<bool> IsSelected { get; }

    CompositeDisposable disposable = new CompositeDisposable();
    public GachaPoolTabViewModel(GachaPoolType type)
    {
        PoolType = type;
        IsSelected = new ReactiveProperty<bool>(false).AddTo(disposable);
    }

    public void SetSelected(bool selected)
    {
        IsSelected.Value = selected;
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
