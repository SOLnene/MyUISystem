using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaPoolTabViewModel : IDisposable
{
    public GachaPoolUIConfig Config { get; }
    public Sprite Icon => Config.tabIcon;
    public ReactiveProperty<bool> IsSelected { get; }

    CompositeDisposable disposable = new CompositeDisposable();
    public GachaPoolTabViewModel(GachaPoolUIConfig config)
    {
        Config = config;
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
