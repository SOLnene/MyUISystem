using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaTopHubViewModel : IDisposable
{
    public IReadOnlyList<GachaPoolTabViewModel> Tabs => tabs;
    readonly List<GachaPoolTabViewModel> tabs;
    public ReactiveCommand<GachaPoolUIConfig> SwitchPoolCommand { get; }

    readonly CompositeDisposable disposable = new CompositeDisposable();
    
    public GachaTopHubViewModel(
        IReadOnlyList<GachaPoolUIConfig> configs,
        IReadOnlyReactiveProperty<GachaPoolUIConfig> currentPoolConfig,
        Action<GachaPoolUIConfig> switchPool)
    {
        SwitchPoolCommand = new ReactiveCommand<GachaPoolUIConfig>().AddTo(disposable);
        SwitchPoolCommand
            .Subscribe(switchPool)
            .AddTo(disposable);
                    
        
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
