using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;


public class BottomHubViewModel
{
    public readonly ReactiveCollection<BottomButtonConfig> Buttons = new();
    public readonly ReactiveProperty<int> GoldCost = new(0);
    public readonly ReactiveProperty<int> ExpGain = new(0);

    private readonly CompositeDisposable _disposables = new();

    public void Dispose() => _disposables.Dispose();
}

public enum BottomBarActionType
{
    None,
    ConfirmEnhance, // 强化
    UseItem,        // 使用物品
    EquipItem,      // 装备
    Filter,         // 筛选
    Back,           // 返回
}

public class BottomButtonConfig
{
    public string Label;              // 按钮显示文字
    public BottomBarActionType Action; // 按钮类型
    public ReactiveCommand Command;   // 按钮点击事件

    public BottomButtonConfig(string label, BottomBarActionType action, Action onClick)
    {
        Label = label;
        Action = action;
        Command = new ReactiveCommand();
        Command.Subscribe(_ => onClick?.Invoke());
    }
}
