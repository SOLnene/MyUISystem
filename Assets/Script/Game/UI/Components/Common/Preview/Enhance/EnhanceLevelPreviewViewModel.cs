using System;
using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using Game.Domain.Enhance;
using UniRx;
using UnityEngine;

public class EnhanceLevelPreviewViewModel
{
    public ReadOnlyReactiveProperty<string> levelText;
    public ReadOnlyReactiveProperty<string> expText;
    public ReadOnlyReactiveProperty<float> expProgress;

    public ReadOnlyReactiveProperty<string> levelUpText;
    public ReadOnlyReactiveProperty<int> levelUpCount;
    public ReadOnlyReactiveProperty<string> expPlusAmountText; 
    public ReadOnlyReactiveProperty<float> previewProgress;

    public ReadOnlyReactiveProperty<bool> isExpAdding;

    // 新增：用于表示等级是否变化
    public ReadOnlyReactiveProperty<bool> isLevelChanged;
    
    CompositeDisposable disposable = new CompositeDisposable();
    
    public EnhanceLevelPreviewViewModel(IEnhanceable model,IReadOnlyReactiveProperty<int> previewExp)
        : this(EnhancementPreviewCalculator.Observe(model, previewExp))
    { }

    internal EnhanceLevelPreviewViewModel(IObservable<EnhancementPreviewData> preview)
    {
        var previewData = preview.Replay(1).RefCount();
        levelText = previewData
            .Select(data => $"Lv.{data.CurrentLevel}")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        //todo:先这样写，之后可能要改成发一个levelstruct而不是两个信号
        expText = previewData
            .Select(data => $"{data.CurrentExp}/{data.CurrentRequiredExp}")
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        expProgress = previewData
            .Select(data => (float)data.CurrentExp / data.CurrentRequiredExp)
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        expPlusAmountText = previewData
            .Select(data => $"+{data.DisplayExp}")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        levelUpCount = previewData
            .Select(data => data.Level.levelUpCount)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        levelUpText = levelUpCount
            .Select(count => count > 0 ? $"+{count}" : "")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        previewProgress = previewData
            .Select(data => (float)(data.CurrentExp + data.DisplayExp) / data.CurrentRequiredExp)
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        // 判断当前添加经验是否大于0
        isExpAdding = previewData
            .Select(data => data.DisplayExp > 0)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        // 只关心预览：判断等级是否发生变化
        isLevelChanged = levelUpCount
            .Select(count => count > 0)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
