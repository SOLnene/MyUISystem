using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaTopHubViewModel : IDisposable
{
    public IReadOnlyReactiveProperty<GachaPoolType> CurrentPoolType => currentPoolType;
    readonly ReactiveProperty<GachaPoolType> currentPoolType;
    public IReadOnlyList<GachaPoolTabViewModel> Tabs => tabs;
    readonly List<GachaPoolTabViewModel> tabs;
    public ReactiveCommand<GachaPoolType> SwitchPoolCommand { get; }

    readonly CompositeDisposable disposable = new CompositeDisposable();
    
    public GachaTopHubViewModel(ReactiveProperty<GachaPoolType> poolType)
    {
        currentPoolType = poolType;
        SwitchPoolCommand = new ReactiveCommand<GachaPoolType>().AddTo(disposable);
        SwitchPoolCommand
            .Subscribe(type =>
                poolType.Value = type).AddTo(disposable);
        
        tabs = new List<GachaPoolTabViewModel>();
        foreach (GachaPoolType type in System.Enum.GetValues(typeof(GachaPoolType)))
        {
            var tab = new GachaPoolTabViewModel(type);
            tabs.Add(tab);

            currentPoolType
                .Select(cur => cur == type)
                .Subscribe(tab.SetSelected)
                .AddTo(disposable);
        }
    }
    
    public void Dispose()
    {
        foreach (var tab in Tabs)
        {
            tab.Dispose();
        }
        SwitchPoolCommand.Dispose();
    }
        
}
