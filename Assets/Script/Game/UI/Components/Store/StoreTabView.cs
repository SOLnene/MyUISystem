using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class StoreTabView : MonoBehaviour
{
    [SerializeField]
    UITabGroup tabGroup;
    [SerializeField, FormerlySerializedAs("goldIcon")]
    Sprite primogemIcon;
    [SerializeField]
    Sprite moraIcon;
    [SerializeField, FormerlySerializedAs("item203Icon")]
    Sprite genesisCrystalIcon;
    [SerializeField, FormerlySerializedAs("fateIcon"), FormerlySerializedAs("starglitterIcon")]
    Sprite starglitterStardustIcon;

    public void Bind(StoreViewModel viewModel)
    {
        var categories = (StoreCategory[])Enum.GetValues(typeof(StoreCategory));
        tabGroup.Bind(CreateOptions(categories), Array.IndexOf(categories, viewModel.CurrentTab.Value), index => viewModel.SetTab(categories[index]));
    }

    IReadOnlyList<UITabOption> CreateOptions(StoreCategory[] categories)
    {
        var options = new List<UITabOption>();
        foreach (StoreCategory category in categories)
        {
            options.Add(CreateOption(category));
        }

        return options;
    }

    UITabOption CreateOption(StoreCategory category)
    {
        switch (category)
        {
            case StoreCategory.Primogem:
                return new UITabOption((int)category, "原石", primogemIcon);
            case StoreCategory.StarglitterStardust:
                return new UITabOption((int)category, "星尘星辉", starglitterStardustIcon);
            case StoreCategory.Mora:
                return new UITabOption((int)category, "摩拉", moraIcon);
            case StoreCategory.GenesisCrystal:
                return new UITabOption((int)category, "礼包", genesisCrystalIcon);
            default:
                return new UITabOption((int)category, category.ToString());
        }
    }
}
