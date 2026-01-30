using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class GameDatabase
{
    static ItemDatabase itemDatabase;
    static CharacterVisualDatabase charaVisualDatabase;
    static GachaPoolDatabase gachaPoolDatabase;
    static GachaPoolUIConfigDatabase gachaPoolUIConfigDatabase;
    public static ItemDatabase ItemDatabase => itemDatabase;
    public static CharacterVisualDatabase CharaVisualDatabase => charaVisualDatabase;
    public static GachaPoolDatabase GachaPoolDatabase => gachaPoolDatabase;
    public static GachaPoolUIConfigDatabase GachaPoolUIConfigDatabase => gachaPoolUIConfigDatabase;
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
        if (itemDatabase == null)
        { 
            Debug.LogError("Failed to load ItemDatabase!");
        }
        else
        {
            Debug.Log("ItemDatabase loaded successfully.");
        }
    }

   
}
