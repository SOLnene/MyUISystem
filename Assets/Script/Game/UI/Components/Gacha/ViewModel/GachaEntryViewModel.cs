using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using UniRx;
using UnityEngine;

/// <summary>
/// 抽卡物品视图模型
/// </summary>
public class GachaEntryViewModel
{
    public string EntryKey { get; private set; }
    public string Name { get; private set; }
    //汇总界面图片
    public Sprite Icon { get;private set; }
    
    //负责加载完成后的刷新
    public readonly ReactiveProperty<Sprite> DetailImage = new ReactiveProperty<Sprite>();
    public int Rarity { get;private set; }

    public GachaEntryType EntryType { get; private set; }
    
    readonly IGachaVisualProvider visualProvider;

    //用于加载完成后刷新
    public readonly Subject<Unit> VisualLoaded = new Subject<Unit>();

    public GachaEntryViewModel(GachaEntry entry,IGachaVisualProvider provider)
    {
        EntryKey = entry.entryKey;
        Name = GetDisplayName(entry);
        visualProvider = provider;
        Rarity = entry.rarity;
        LoadVisual(entry).Forget();
        EntryType = entry.entryType;
    }

    string GetDisplayName(GachaEntry entry)
    {
        switch (entry.entryType)
        {
            case GachaEntryType.Character:
                CharacterDefinition character = GameDatabase.CharacterDatabase.Get(entry.entryKey);
                return character != null ? character.displayName : entry.entryKey;
            case GachaEntryType.Equip:
                ItemDefinition item = GameDatabase.ItemDatabase.GetItemByKey(entry.entryKey);
                return item != null ? item.itemName : entry.entryKey;
            default:
                return entry.entryKey;
        }
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
