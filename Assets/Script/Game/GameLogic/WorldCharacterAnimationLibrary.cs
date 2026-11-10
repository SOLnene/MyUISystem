using System;
using UnityEngine;

internal sealed class WorldCharacterAnimationLibrary : ScriptableObject
{
    [SerializeField] WorldCharacterResource[] characters = Array.Empty<WorldCharacterResource>();

    internal WorldCharacterResource Find(string key)
    {
        foreach (var character in characters)
            if (character.characterKey == key) return character;
        return null;
    }
}

[Serializable]
internal sealed class WorldCharacterResource
{
    [SerializeField] internal string characterKey;
    [SerializeField] internal string modelAddress;
    [SerializeField] internal string motionAddress;
    [SerializeField] internal Vector3 localPosition;
    [SerializeField] internal Vector3 localEulerAngles;
    [SerializeField] internal Vector3 localScale = Vector3.one;
}
