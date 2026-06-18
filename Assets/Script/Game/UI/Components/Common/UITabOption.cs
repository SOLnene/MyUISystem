using UnityEngine;

public readonly struct UITabOption
{
    public readonly int Id;
    public readonly string Label;
    public readonly Sprite Icon;

    public UITabOption(int id, string label, Sprite icon = null)
    {
        Id = id;
        Label = label;
        Icon = icon;
    }
}
