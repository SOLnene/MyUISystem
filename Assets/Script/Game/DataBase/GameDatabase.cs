using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using Game.Domain.Promote;
using UnityEngine;
/// <summary>
/// 所有database
/// </summary>
public static class GameDatabase
{
    static ItemDatabase itemDatabase;
    static CharacterVisualDatabase charaVisualDatabase;
    static GachaPoolDatabase gachaPoolDatabase;
    static GachaPoolUIConfigDatabase gachaPoolUIConfigDatabase;
    static CharacterDatabase characterDatabase;
    static PromoteDatabase promoteDatabase;
    
    public static ItemDatabase ItemDatabase => itemDatabase;
    public static CharacterVisualDatabase CharaVisualDatabase => charaVisualDatabase;
    public static GachaPoolDatabase GachaPoolDatabase => gachaPoolDatabase;
    public static GachaPoolUIConfigDatabase GachaPoolUIConfigDatabase => gachaPoolUIConfigDatabase;
    
    public static CharacterDatabase CharacterDatabase => characterDatabase;
    
    public static PromoteDatabase PromoteDatabase => promoteDatabase;
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
    }

   
}
