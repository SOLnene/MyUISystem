using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Database/CharacterVisual Database")]
public class CharacterVisualDatabase : ScriptableObject
{
    [SerializeField]
    List<CharacterVisualDefinition> visuals = new();

    Dictionary<string, CharacterVisualDefinition> visualMap;
    
    public CharacterVisualDefinition Get(string characterKey)
    {
        if (visualMap == null)
        {
            visualMap = new Dictionary<string, CharacterVisualDefinition>();
            foreach (var v in visuals)
            {
                visualMap[v.characterKey] = v;
            }
        }

        if (!visualMap.TryGetValue(characterKey, out var def))
        {
            Debug.LogWarning($"CharacterVisualDatabase: 找不到 key={characterKey}");
            return null;
        }
        return def;
    }

    public void Add(CharacterVisualDefinition def)
    {
        if (!visuals.Contains(def))
        {
            visuals.Add(def);
            visualMap?.Add(def.characterKey,def);
        }
    }

    public void Remove(CharacterVisualDefinition def)
    {
        visuals.Remove(def);
        visualMap?.Remove(def.characterKey);
    }

    public IReadOnlyList<CharacterVisualDefinition> AllVisuals => visuals;
}
