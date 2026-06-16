using System.Collections.Generic;
using Game.Domain.Character;

public class CharacterRepository
{
    readonly List<CharacterModel> characters = new List<CharacterModel>();
    readonly Dictionary<string, CharacterModel> characterMap = new Dictionary<string, CharacterModel>();

    public IReadOnlyList<CharacterModel> Characters => characters;

    public CharacterModel Add(CharacterDefinition definition)
    {
        if (characterMap.TryGetValue(definition.key, out CharacterModel existing))
        {
            return existing;
        }

        CharacterModel model = CharacterFactory.Create(definition, 1);
        characters.Add(model);
        characterMap.Add(definition.key, model);
        return model;
    }

    public CharacterModel GetByKey(string key)
    {
        characterMap.TryGetValue(key, out CharacterModel model);
        return model;
    }
}
