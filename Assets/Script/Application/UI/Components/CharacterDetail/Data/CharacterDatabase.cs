using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game/Database/Character Database")]
public class CharacterDatabase : ScriptableObject
{
    [SerializeField]
    List<CharacterDefinition> characters = new();

    Dictionary<string, CharacterDefinition> characterMap;
    
    public CharacterDefinition Get(string characterKey)
    {
        if (characterMap == null)
        {
            characterMap = new Dictionary<string, CharacterDefinition>();
            foreach (var v in characters)
            {
                characterMap[v.key] = v;
            }
        }

        if (!characterMap.TryGetValue(characterKey, out var def))
        {
            Debug.LogWarning($"CharacterDatabase: 找不到 key={characterKey}");
            return null;
        }
        return def;
    }

    public void Add(CharacterDefinition def)
    {
        if (!characters.Contains(def))
        {
            characters.Add(def);
            characterMap?.Add(def.key,def);
        }
    }

    public void Remove(CharacterDefinition def)
    {
        characters.Remove(def);
        characterMap?.Remove(def.key);
    }

    public IReadOnlyList<CharacterDefinition> AllCharacter => characters;
}
