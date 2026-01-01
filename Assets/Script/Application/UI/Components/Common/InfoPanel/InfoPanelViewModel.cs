using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
public class InfoPanelViewModel: IDisposable
{
    public readonly ReactiveProperty<string> name = new();
    public readonly ReactiveProperty<string> desc = new();
    public readonly ReactiveProperty<int> stars = new();
    public readonly ReactiveProperty<Color> color = new();
    public readonly ReactiveProperty<string> iconPath = new();
    public readonly ReactiveProperty<string> displayMainText = new();
    
    CompositeDisposable disposables = new();
    
    public void Bind(ItemViewModel vm)
    {
        vm.name.Subscribe(x => name.Value = x).AddTo(disposables);
        vm.desc.Subscribe(x => desc.Value = x).AddTo(disposables);
        vm.iconPath.Subscribe(x => iconPath.Value = x).AddTo(disposables);
        vm.star.Subscribe(x => stars.Value = x).AddTo(disposables);
        vm.color.Subscribe(x => color.Value = x).AddTo(disposables);
        vm.displayMainText.Subscribe(text => displayMainText.Value = text).AddTo(disposables);
    }
    public void Dispose()
    {
        disposables.Dispose();
        
    }
}
