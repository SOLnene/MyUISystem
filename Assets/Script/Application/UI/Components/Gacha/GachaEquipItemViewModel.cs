using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

public class GachaEquipItemViewModel
{
    public ReactiveProperty<string> Name { get; } = new ReactiveProperty<string>();
    public ReactiveProperty<Sprite> Icon { get; } = new ReactiveProperty<Sprite>();
    public ReactiveProperty<int> Rarity { get; } = new ReactiveProperty<int>();

    public GachaEquipItemViewModel(EquipItem item)
    {
        Name.Value = item.ItemName;
        Rarity.Value = (int)item.ItemRarity;
        LoadIconAsync(item.Key).Forget();
        
    }

    //todo:统一管理 Addressable 名字    
    private async UniTask LoadIconAsync(string equipName)
    {
        if (string.IsNullOrEmpty(equipName))
            return;

        string path = $"ui_gacha_equipicon_{equipName}".ToLower();
        try
        {
            Icon.Value = await ResourceManager.Instance.LoadAssetAsync<Sprite>(path);
            
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SingleResultView] 加载图标失败：{path} - {e.Message}");
            return ;
        }
    }


}
