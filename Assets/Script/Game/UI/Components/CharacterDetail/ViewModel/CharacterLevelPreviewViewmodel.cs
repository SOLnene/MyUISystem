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
    public ReadOnlyReactiveProperty<string> previewExpText;
    public ReadOnlyReactiveProperty<float> previewProgress;
    
    public CharacterLevelPreviewViewmodel(IEnhanceable model,IReadOnlyReactiveProperty<int> previewExp)
    {
        levelText = model.LevelRP
            .Select(l => $"Lv.{l}")
            .ToReadOnlyReactiveProperty();
        
        expText = model.LevelRP
            .CombineLatest(model.ExpRP,
                (level, exp) =>
                {
                    int max = model.LevelSystem.GetExpRequired(level);
                    return $"{exp}/{max}";
                })
            .ToReadOnlyReactiveProperty();
        
        expProgress = model.LevelRP
            .CombineLatest(model.ExpRP,
                (level, exp) =>
                {
                    int max = model.LevelSystem.GetExpRequired(level);
                    return (float)exp / max;
                })
            .ToReadOnlyReactiveProperty();
        
        var previewData = previewExp
            .Select(addedExp => model.LevelSystem.GetPreviewWithExp(addedExp))
            .Share();
        
        levelUpText = previewData
            .Select(data => data.levelUpCount > 0 ? $"+{data.levelUpCount}" : "")
            .ToReadOnlyReactiveProperty();

        previewExpText = previewExp
            .Select(exp => $"+{previewExp.Value}")
            .ToReadOnlyReactiveProperty();

        
    }
        
}
