using System;
using UnityEngine;
using UnityEngine.UI;

internal static class GachaWeaponImageLayout
{
    internal static Vector2 Fit(Image image, GachaEntryViewModel entry, Vector2 viewport,
        Vector2 center, bool overview)
    {
        // Visual loading is asynchronous; a reused card can bind before its sprite arrives.
        if (image.sprite == null) return center;
        var definition = entry.Item?.ItemDefinition as EquipDefinition
            ?? GameDatabase.ItemDatabase.GetItemByKey(entry.EntryKey) as EquipDefinition;
        if (definition == null)
            throw new InvalidOperationException("Missing weapon presentation definition: " + entry.EntryKey);
        Vector2 fraction = definition.weaponType switch
        {
            "Catalyst" => overview ? new Vector2(.88f, .36f) : new Vector2(.38f, .48f),
            "Bow" => overview ? new Vector2(.95f, .66f) : new Vector2(.50f, .70f),
            _ => overview ? new Vector2(1.15f, .86f) : new Vector2(.50f, .80f)
        };
        Vector2 source = image.sprite.rect.size;
        Rect content = entry.EntryKey == "Catalyst_Pulpfic"
            ? new Rect(153f / 355f, 300f / 531f, 200f / 355f, 228f / 531f)
            : new Rect(0, 0, 1, 1);
        // Pulpfic's original PNG contains a large transparent canvas. Fit its visible book, preserving the source asset.
        Vector2 visible = Vector2.Scale(source, content.size);
        Vector2 available = Vector2.Scale(viewport, fraction);
        float scale = Mathf.Min(available.x / visible.x, available.y / visible.y);
        Vector2 offset = Vector2.Scale(content.center - Vector2.one * .5f, source) * scale;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.one * .5f;
        rect.anchorMax = Vector2.one * .5f;
        rect.pivot = Vector2.one * .5f;
        rect.sizeDelta = source * scale;
        rect.anchoredPosition = center - offset;
        image.preserveAspect = true;
        return rect.anchoredPosition;
    }
}
