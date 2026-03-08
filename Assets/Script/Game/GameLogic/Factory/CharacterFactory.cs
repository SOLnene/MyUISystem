using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public static class CharacterFactory
{
    public static CharacterModel Create(CharacterDefinition definition, int level = 1,int exp = 0)
    {
        return new CharacterModel(
            definition,level,exp
            );
    }
}
