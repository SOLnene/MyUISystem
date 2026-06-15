using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Database/Gacha Pool UI Config Database")]
public class GachaPoolUIConfigDatabase : ScriptableObject
{
    public GachaPoolUIConfig defaultConfig;
    public List<GachaPoolUIConfig> configs = new();
    Dictionary<string, GachaPoolUIConfig> dict;
    public IReadOnlyList<GachaPoolUIConfig> Configs => configs;
    
    public GachaPoolUIConfig GetConfig(string key)
    {
        EnsureDictBuilt();
        dict.TryGetValue(key, out var result);
        return result;
    }
    
    private void EnsureDictBuilt()
    {
        if (dict != null) return;

        dict = new Dictionary<string, GachaPoolUIConfig>();
        foreach (var def in configs)
        {
            if (def == null)
            {
                Debug.LogWarning("GachaPoolUIConfigDatabase 中存在空引用 GachaPoolUIConfig！");
                continue;
            }

            if (dict.ContainsKey(def.gachaKey))
            {
                Debug.LogWarning($"重复的 GachaPoolUIConfig key: {def.gachaKey} ");
                continue;
            }

            dict.Add(def.gachaKey, def);
        }
    }
}
