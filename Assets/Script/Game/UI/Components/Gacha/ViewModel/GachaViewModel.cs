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
    ├── 和业务层交互
    ├── 把“原始结果”转成 ViewModel
    ├── 创建 Session
    └── 通知 UI：有一次新抽卡开始了

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
    // isDrawing:
    // - 流程互斥（防止并行 DrawAsync）
    // - UI 防重入（按钮灰掉）
    // ⚠ 后续可能拆为 isBusy / canInput
    public ReactiveProperty<bool> isDrawing = new ReactiveProperty<bool>(false);
    GachaSessionViewModel currentSession;
    
    public ReactiveProperty<GachaPoolType> CurrentPoolType { get; }
        = new ReactiveProperty<GachaPoolType>(GachaPoolType.Character);
    
    public Subject<GachaSessionViewModel> OnSessionStarted { get; } = new Subject<GachaSessionViewModel>();

    readonly IGachaService gachaService;
    readonly IGachaVisualProvider visualProvider;
    
    public GachaViewModel(IGachaService service,IGachaVisualProvider provider)
    {
        gachaService = service;
        visualProvider = provider;

        //currentIndex.Subscribe(_ => UpdateHasNext()).AddTo(disposable);
        lastDrawnItems.ObserveCountChanged().Subscribe(_ => UpdateHasNext()).AddTo(disposable);
        
        drawCommand
            .Where(_ => !isDrawing.Value &&
                        (currentSession == null || currentSession.Phase.Value == GachaSessionPhase.Finished))
            .Subscribe(
                count =>
                {
                    //启动异步
                    _ = DrawAsync(count);
                }).AddTo(disposable);
        CurrentPoolType.Subscribe(
            type =>Debug.Log($"切换卡池到 {type}"))
            .AddTo(disposable);
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
        
        currentSession?.Dispose();
        currentSession = new GachaSessionViewModel(lastDrawnItems);
        isDrawing.Value = false;
        //开始展示流程
        //TODO：再包一层
        //UIManager.Instance.Open(UIType.GachaResultDetailPopup,sessionVM);
        //ShowNext();
        OnSessionStarted.OnNext(currentSession);
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
        
    }
    
    /*public bool HasNext()
    {
        hasNext.Value = currentIndex.Value >= 0 && currentIndex.Value < lastDrawnItems.Count - 1;
        return currentIndex.Value >= 0 && currentIndex.Value < lastDrawnItems.Count - 1;
    }*/
    
    void UpdateHasNext()
    {
       
    }
    
    
    public void CloseResult()
    {
        
    }
    
    public void Dispose()
    {
        currentSession?.Dispose();
        disposable.Dispose();
    }
}
