using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GachaVisualProvider : IGachaVisualProvider
{
    CharacterVisualDatabase characterDB;
    
    public GachaVisualProvider(CharacterVisualDatabase db)
    {
        characterDB = db;
    }

    public GachaVisual GetVisual(GachaEntry entry)
    {
        switch (entry.entryType)
        {
            case GachaEntryType.Character:
                return GetCharacterVisual(entry.entryKey);
            case GachaEntryType.Equip:
                return GetEquipVisual(entry.entryKey);
        }
        return null;
    }

    GachaVisual GetCharacterVisual(string key)
    {
        var def = characterDB.Get(key);
        if (def == null)
        {
            Debug.LogError( $"Cannot find visual definition for key: {key}");
            return null;
        }

        return new GachaVisual
        {
            IconPath = CharacterVisualAddressResolver.ResolveIcon(key),
            DetailImagePath = CharacterVisualAddressResolver.ResolveDetailImage(key)
        };
    }
    
    GachaVisual GetEquipVisual(string key)
    {
        return new GachaVisual
        {
            IconPath = $"ui_gacha_equipicon_{key}",
            DetailImagePath = $"ui_gacha_equipicon_{key}"
        };
    }
}
