using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using Game.Domain.Promote;
using Newtonsoft.Json;
using UnityEngine;
/// <summary>
/// 所有database
/// </summary>
public static class GameDatabase
{
    const string StoreConfigAddress = "config/store";

    static ItemDatabase itemDatabase;
    static CharacterVisualDatabase charaVisualDatabase;
    static GachaPoolDatabase gachaPoolDatabase;
    static GachaPoolUIConfigDatabase gachaPoolUIConfigDatabase;
    static CharacterDatabase characterDatabase;
    static PromoteDatabase promoteDatabase;
    static StoreDatabase storeDatabase;
    static StoreConfigDatabase storeConfigDatabase;
    
    public static ItemDatabase ItemDatabase => itemDatabase;
    public static CharacterVisualDatabase CharaVisualDatabase => charaVisualDatabase;
    public static GachaPoolDatabase GachaPoolDatabase => gachaPoolDatabase;
    public static GachaPoolUIConfigDatabase GachaPoolUIConfigDatabase => gachaPoolUIConfigDatabase;
    
    public static CharacterDatabase CharacterDatabase => characterDatabase;
    
    public static PromoteDatabase PromoteDatabase => promoteDatabase;
    public static StoreDatabase StoreDatabase => storeDatabase;
    public static StoreConfigDatabase StoreConfigDatabase => storeConfigDatabase;
    public static async UniTask Init()
    {
        if (itemDatabase != null)
        {
            return;
        }

        itemDatabase = await ResourceManager.Instance.LoadAssetAsync<ItemDatabase>("itemdatabase");
        charaVisualDatabase = await ResourceManager.Instance.LoadAssetAsync<CharacterVisualDatabase>("charactervisualdatabase");
        gachaPoolDatabase = await ResourceManager.Instance.LoadAssetAsync<GachaPoolDatabase>("gachapooldatabase");
        gachaPoolUIConfigDatabase = await ResourceManager.Instance.LoadAssetAsync<GachaPoolUIConfigDatabase>("gachapooluiconfigdatabase");
        characterDatabase = await ResourceManager.Instance.LoadAssetAsync<CharacterDatabase>("characterdatabase");
        promoteDatabase = await ResourceManager.Instance.LoadAssetAsync<PromoteDatabase>("promotedatabase");
        storeDatabase = await ResourceManager.Instance.LoadAssetAsync<StoreDatabase>("storeitemdatabase");
        storeConfigDatabase = await LoadStoreConfigDatabase();
    }

    static async UniTask<StoreConfigDatabase> LoadStoreConfigDatabase()
    {
        TextAsset configAsset = await ResourceManager.Instance
            .LoadAssetAsync<TextAsset>(StoreConfigAddress);
        if (configAsset == null)
        {
            throw new InvalidOperationException(
                $"Store config asset is unavailable: {StoreConfigAddress}.");
        }

        StoreConfigData config;
        try
        {
            config = JsonConvert.DeserializeObject<StoreConfigData>(configAsset.text);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Store config parse failed.", exception);
        }

        if (!StoreConfigValidator.TryValidate(config, itemDatabase))
        {
            throw new InvalidOperationException("Store config validation failed.");
        }

        return new StoreConfigDatabase(config.items);
    }
}
