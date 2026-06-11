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
    internal readonly ReactiveProperty<bool> showStatArea = new();
    internal readonly ReactiveProperty<string> mainStatLabel = new();
    internal readonly ReactiveProperty<string> mainStatValue = new();
    internal readonly ReactiveProperty<string> subStatLabel = new();
    internal readonly ReactiveProperty<string> subStatValue = new();
    
    CompositeDisposable disposables = new();
    
    public void Bind(ItemViewModel vm)
    {
        disposables.Clear();
        vm.name.Subscribe(x => name.Value = x).AddTo(disposables);
        vm.desc.Subscribe(x => desc.Value = x).AddTo(disposables);
        vm.iconPath.Subscribe(x => iconPath.Value = x).AddTo(disposables);
        vm.star.Subscribe(x => stars.Value = x).AddTo(disposables);
        vm.color.Subscribe(x => color.Value = x).AddTo(disposables);
        if (vm.Model is EquipItem equipItem)
        {
            showStatArea.Value = true;
            mainStatLabel.Value = "基础攻击力";
            subStatLabel.Value = "暴击伤害";

            if (vm is EquipItemViewModel equipVM)
            {
                equipVM.attack.Select(value => value.ToString()).Subscribe(value => mainStatValue.Value = value).AddTo(disposables);
                equipVM.critical.Select(value => value.ToString("P1")).Subscribe(value => subStatValue.Value = value).AddTo(disposables);
            }
            else
            {
                mainStatValue.Value = equipItem.GetDisplayMainStatText();
                subStatValue.Value = equipItem.GetCriticalDamage().ToString("P1");
            }
        }
        else
        {
            showStatArea.Value = false;
            mainStatLabel.Value = string.Empty;
            mainStatValue.Value = string.Empty;
            subStatLabel.Value = string.Empty;
            subStatValue.Value = string.Empty;
        }
    }
    public void Dispose()
    {
        disposables.Dispose();
        
    }
}
