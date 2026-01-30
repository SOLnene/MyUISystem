using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
/*
 * 【业务层】
GachaService / GachaDomain
    └── 负责随机、保底、掉落规则

【流程层】
GachaViewModel
    ├── 执行抽卡
    ├── 控制 SingleResult 流程
    ├── 处理 Skip
    └── 决定“什么时候进入 Result”

【展示层】
SingleResultViewModel   ← 单个逐个展示
GachaResultViewModel    ← 汇总展示

 */
public class GachaViewModel : IDisposable
{
    CompositeDisposable disposable = new CompositeDisposable();
    // 抽卡命令，参数为抽卡数量
    public ReactiveCommand<int> drawCommand = new ReactiveCommand<int>();
    // 最近一次抽到的物品列表
    public ReactiveCollection<GachaEntryViewModel> lastDrawnItems = new ReactiveCollection<GachaEntryViewModel>();
    // 是否正在抽卡
    public ReactiveProperty<bool> isDrawing = new ReactiveProperty<bool>(false);
    // 当前选中的物品索引
    public ReactiveProperty<int> currentIndex = new ReactiveProperty<int>(-1);
    //是否有下一个
    public ReactiveProperty<bool> hasNext = new ReactiveProperty<bool>(false);
    
    public ReactiveProperty<GachaPoolType> CurrentPoolType { get; }
        = new ReactiveProperty<GachaPoolType>(GachaPoolType.Character);
    
    public Subject<GachaSessionViewModel> OnSessionStarted { get; } = new Subject<GachaSessionViewModel>();
    /// <summary>
    /// 抽卡流程控制
    /// </summary>
    public GachaSessionViewModel sessionVM;

    readonly IGachaService gachaService;
    readonly IGachaVisualProvider visualProvider;
    
    public GachaViewModel(IGachaService service,IGachaVisualProvider provider)
    {
        gachaService = service;
        visualProvider = provider;
        drawCommand
            .Where(_ => !isDrawing.Value)
            .Subscribe(
                count =>
                {
                    //启动异步
                    _ = DrawAsync(count);
                }).AddTo(disposable);
        
    }

    async UniTask DrawAsync(int count)
    {
        isDrawing.Value = true;
        lastDrawnItems.Clear();

        var poolType = CurrentPoolType.Value; // 快照
        var result = gachaService.Draw(count,poolType);

        foreach (var e in result.Entries)
        {
            var vm = new GachaEntryViewModel(e,visualProvider);
            lastDrawnItems.Add(vm);
        }
        
        isDrawing.Value = false;
        
        sessionVM = new GachaSessionViewModel(lastDrawnItems);
        //开始展示流程
        //TODO：再包一层
        //UIManager.Instance.Open(UIType.GachaResultDetailPopup,sessionVM);
        //ShowNext();
        OnSessionStarted.OnNext(sessionVM);
    }

    public void SwitchPool(GachaPoolType type)
    {
        if (CurrentPoolType.Value == type)
            return;

        CurrentPoolType.Value = type;
        Debug.Log($"切换卡池：{type}");
    }
    
    public void ShowNext()
    {
        // 当 currentIndex 为 -1 时，说明是第一次显示 → 设置为 0
        if (currentIndex.Value < 0)
        {
            currentIndex.Value = 0;
            return;
        }
        
        if(currentIndex.Value<0||currentIndex.Value>lastDrawnItems.Count)
        {
            return;
        }
        currentIndex.Value++;
    }
    
    public bool HasNext()
    {
        hasNext.Value = currentIndex.Value >= 0 && currentIndex.Value < lastDrawnItems.Count - 1;
        return currentIndex.Value >= 0 && currentIndex.Value < lastDrawnItems.Count - 1;
    }
    
    public void ShowNextOrClose()
    {
        if (HasNext())
        {
            ShowNext();
        }
        else
        {
            CloseResult();
        }
    }
    
    public void CloseResult()
    {
        currentIndex.Value = -1;
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
