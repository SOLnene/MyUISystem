using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Database/Gacha Pool Database")]
public class GachaPoolDatabase: ScriptableObject
{
    public List<GachaDefinition> pools = new ();

    Dictionary<string, GachaDefinition> dict;
    public IReadOnlyList<GachaDefinition> AllPools => pools;
    public GachaDefinition GetPool(string key)
    {
        EnsureDictBuilt();
        dict.TryGetValue(key, out var result);
        return result;
    }

    private void EnsureDictBuilt()
    {
        if (dict != null) return;

        dict = new Dictionary<string, GachaDefinition>();
        foreach (var def in pools)
        {
            if (def == null)
            {
                Debug.LogWarning("GachaPoolDatabase 中存在空引用 GachaDefinition！");
                continue;
            }

            if (dict.ContainsKey(def.gachaKey))
            {
                Debug.LogWarning($"重复的 GachaDefinition key: {def.gachaKey} ");
                continue;
            }

            dict.Add(def.gachaKey, def);
        }
    }
}

