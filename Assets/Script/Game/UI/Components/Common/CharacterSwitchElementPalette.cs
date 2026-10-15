using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSwitchElementPalette", menuName = "Game/UI/ModelViewer/Character Switch Element Palette")]
public sealed class CharacterSwitchElementPalette : ScriptableObject
{
    [Serializable]
    sealed class Entry
    {
        public string element;
        [ColorUsage(false)] public Color color;

        internal Entry(string element, Color color)
        {
            this.element = element;
            this.color = color;
        }
    }

    [SerializeField] List<Entry> entries = new List<Entry>
    {
        new Entry("Anemo", new Color(0.47f, 1f, 0.82f)),
        new Entry("Geo", new Color(1f, 0.83f, 0.48f)),
        new Entry("Electro", new Color(0.77f, 0.60f, 1f)),
        new Entry("Dendro", new Color(0.74f, 1f, 0.46f)),
        new Entry("Hydro", new Color(0.41f, 0.78f, 1f)),
        new Entry("Pyro", new Color(1f, 0.60f, 0.41f)),
        new Entry("Cryo", new Color(0.72f, 0.96f, 1f))
    };
    [SerializeField, ColorUsage(false)] Color defaultColor = Color.white;
    [SerializeField, Range(0f, 1f)] float coreWhiteMix = 0.65f;
    [SerializeField, Range(0f, 1f)] float moteWhiteMix = 0.35f;

    internal readonly struct Colors
    {
        internal readonly Color Core;
        internal readonly Color Glow;
        internal readonly Color Motes;

        internal Colors(Color core, Color glow, Color motes)
        {
            Core = core;
            Glow = glow;
            Motes = motes;
        }
    }

    internal Colors Resolve(string element)
    {
        Color tone = defaultColor;
        if (!string.IsNullOrWhiteSpace(element))
        {
            string key = element.Trim();
            foreach (Entry entry in entries)
            {
                if (string.Equals(entry.element, key, StringComparison.OrdinalIgnoreCase))
                {
                    tone = entry.color;
                    break;
                }
            }
        }

        // 元素配置只控制色相；最大通道归一化，让连续换色保留各层原有亮度。
        float peak = tone.maxColorComponent;
        tone = peak > 0f
            ? new Color(tone.r / peak, tone.g / peak, tone.b / peak, 1f)
            : Color.white;
        return new Colors(
            Color.Lerp(tone, Color.white, coreWhiteMix),
            tone,
            Color.Lerp(tone, Color.white, moteWhiteMix));
    }
}
