using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public class GachaViewModel : IDisposable
{
    CompositeDisposable disposable = new CompositeDisposable();
    // 抽卡命令，参数为抽卡数量
    public ReactiveCommand<int> drawCommand = new ReactiveCommand<int>();
    // 最近一次抽到的物品列表
    public ReactiveCollection<GachaEquipItemViewModel> lastDrawnItems = new ReactiveCollection<GachaEquipItemViewModel>();
    // 是否正在抽卡
    public ReactiveProperty<bool> isDrawing = new ReactiveProperty<bool>(false);
    // 当前选中的物品索引
    public ReactiveProperty<int> currentIndex = new ReactiveProperty<int>(-1);
    //是否有下一个
    public ReactiveProperty<bool> hasNext = new ReactiveProperty<bool>(false);
    public SingleResultViewModel singleResultVM;
    
    public GachaViewModel()
    {
        drawCommand
            .Where(_ => !isDrawing.Value)
            .Subscribe(
                count =>
                {
                    //启动异步
                    _ = DrawAsync(count);
                }).AddTo(disposable);

        singleResultVM = new SingleResultViewModel(this);
    }

    async UniTask DrawAsync(int count)
    {
        isDrawing.Value = true;
        lastDrawnItems.Clear();
        for (int i = 0; i < count; i++)
        {
            var item = ItemFactory.CreateRandomWeaponItem();
            GameContext.Instance.BackpackVM.AddItem(item);
            var viewModel = new GachaEquipItemViewModel(item);
            lastDrawnItems.Add(viewModel);
            //模拟抽卡延时
            //await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
        }
        isDrawing.Value = false;
        ShowNext();
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
        singleResultVM.Dispose();
    }
}
