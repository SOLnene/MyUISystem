using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public interface ICharacterDefinitionResolver 
{
    void Resolve(CharacterDefinition def);
}
