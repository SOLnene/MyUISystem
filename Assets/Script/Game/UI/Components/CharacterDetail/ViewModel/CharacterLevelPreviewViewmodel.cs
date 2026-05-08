using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;

public class CharacterLevelPreviewViewmodel
{
    public ReadOnlyReactiveProperty<string> levelText;
    public ReadOnlyReactiveProperty<string> expText;
    public ReadOnlyReactiveProperty<float> expProgress;

    public ReadOnlyReactiveProperty<string> levelUpText;
    public ReadOnlyReactiveProperty<string> expPlusAmountText; 
    public ReadOnlyReactiveProperty<float> previewProgress;

    public ReadOnlyReactiveProperty<bool> isExpAdding;

    // 新增：用于表示等级是否变化
    public ReadOnlyReactiveProperty<bool> isLevelChanged;
    
    CompositeDisposable disposable = new CompositeDisposable();
    
    public CharacterLevelPreviewViewmodel(IEnhanceable model,IReadOnlyReactiveProperty<int> previewExp)
    {
        levelText = model.LevelRP
            .Select(l => $"Lv.{l}")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        expText = model.LevelRP
            .CombineLatest(model.ExpRP,
                (level, exp) =>
                {
                    int max = model.LevelSystem.GetExpRequired(level);
                    Debug.Log($"currentlevel:{level}currentexp:{exp}");
                    return $"{exp}/{max}";
                })
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        expProgress = model.LevelRP
            .CombineLatest(model.ExpRP,
                (level, exp) =>
                {
                    int max = model.LevelSystem.GetExpRequired(level);
                    return (float)exp / max;
                })
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        var previewData = previewExp
            .Select(addedExp => model.LevelSystem.GetPreviewWithExp(addedExp,
                model.GetCurrentMaxLevel() ))
            .Share();

        expPlusAmountText = previewExp
            .Select(exp => $"+{exp}")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        levelUpText = previewData
            .Select(data => data.levelUpCount > 0 ? $"+{data.levelUpCount}" : "")
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        previewProgress = Observable
            .CombineLatest(
                model.LevelRP,
                model.ExpRP,
                previewExp,
                (level, exp, added) =>
                {
                    int max = model.LevelSystem.GetExpRequired(level);
                    return (float)(exp + added) / max;
                })
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
        
        // 判断当前添加经验是否大于0
        isExpAdding = previewExp
            .Select(addedExp => addedExp > 0)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);

        // 只关心预览：判断等级是否发生变化
        isLevelChanged = previewData
            .Select(preview => preview.levelUpCount > 0)
            .ToReadOnlyReactiveProperty()
            .AddTo(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
