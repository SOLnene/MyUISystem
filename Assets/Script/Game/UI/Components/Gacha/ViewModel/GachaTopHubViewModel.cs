using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaTopHubViewModel : IDisposable
{
    public IReadOnlyReactiveProperty<GachaPoolUIConfig> CurrentPoolConfig => currentPoolConfig;
    readonly ReactiveProperty<GachaPoolUIConfig> currentPoolConfig;
    public IReadOnlyList<GachaPoolTabViewModel> Tabs => tabs;
    readonly List<GachaPoolTabViewModel> tabs;
    public ReactiveCommand<GachaPoolUIConfig> SwitchPoolCommand { get; }

    readonly CompositeDisposable disposable = new CompositeDisposable();
    
    public GachaTopHubViewModel(
        ReactiveProperty<GachaPoolUIConfig> poolConfig,
        IReadOnlyList<GachaPoolUIConfig> configs)
    {
        currentPoolConfig = poolConfig;
        SwitchPoolCommand = new ReactiveCommand<GachaPoolUIConfig>().AddTo(disposable);
        SwitchPoolCommand
            .Subscribe(config =>
            {
                poolConfig.Value = config;
            }).AddTo(disposable);
                    
        
        tabs = new List<GachaPoolTabViewModel>();
        foreach (var config in configs)
        {
            var tab = new GachaPoolTabViewModel(config);
            tabs.Add(tab);

            currentPoolConfig
                .Select(cur => cur == config)
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
