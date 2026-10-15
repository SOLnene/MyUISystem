using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterVisualAddressResolver
{
    // Square roster portraits use separate keys from the vertical gacha result cards.
    internal static string ResolveAvatarIcon(string characterId)
    {
        return $"chara_ui_avataricon_{characterId.ToLowerInvariant()}";
    }

    public static string ResolveIcon(string characterId)
    {
        return $"chara_ui_gacha_avataricon_{characterId.ToLower()}";
    }

    public static string ResolveDetailImage(string characterId)
    {
        return $"chara_ui_gacha_avatarimg_{characterId.ToLower()}";
    }
}
