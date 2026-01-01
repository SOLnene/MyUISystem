using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class GameDatabase
{
    static ItemDatabase itemDatabase;
    public static ItemDatabase ItemDatabase => itemDatabase;

    public static async UniTask Init()
    {
        if (itemDatabase != null)
        {
            return;
        }

        itemDatabase = await ResourceManager.Instance.LoadAssetAsync<ItemDatabase>("itemDatabase");
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
