using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

/// <summary>
/// 抽卡物品视图模型
/// </summary>
public class GachaEntryViewModel
{
    //todo:生命周期内不会改变的属性，不用reactiveproperty 这里实际上是key而不是name
    public string Name { get; private set; }
    public Sprite Icon { get;private set; }
    //负责加载完成后的刷新
    public readonly ReactiveProperty<Sprite> DetailImage = new ReactiveProperty<Sprite>();
    public int Rarity { get;private set; }

    public GachaEntryType EntryType { get; private set; }
    
    readonly IGachaVisualProvider visualProvider;

    //用于加载完成后刷新
    public readonly Subject<Unit> VisualLoaded = new Subject<Unit>();
    //TODO:接入characterdefinition
    public GachaEntryViewModel(GachaEntry entry,IGachaVisualProvider provider)
    {
        Name = entry.entryKey;
        visualProvider = provider;
        Rarity = entry.rarity;
        LoadVisual(entry).Forget();
        EntryType = entry.entryType;
    }

    async UniTask LoadVisual(GachaEntry entry)
    {
        var visual = visualProvider.GetVisual(entry);
        if (visual == null)
        {
            return;
        }

        Icon = await ResourceManager.Instance.LoadAssetAsync<Sprite>(visual.IconPath);
        
        DetailImage.Value = await ResourceManager.Instance.LoadAssetAsync<Sprite>(visual.DetailImagePath);
        VisualLoaded.OnNext(Unit.Default);
    }
    
    
}
