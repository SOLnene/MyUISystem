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

    static readonly IReadOnlyList<UITabOption> FallbackOptions = new[]
    {
        new UITabOption((int)StoreCategory.Primogem, "原石"),
        new UITabOption((int)StoreCategory.StarglitterStardust, "星尘星辉"),
        new UITabOption((int)StoreCategory.Mora, "摩拉"),
        new UITabOption((int)StoreCategory.GenesisCrystal, "礼包"),
    };

    public void Bind(StoreViewModel viewModel)
    {
        IReadOnlyList<UITabOption> options = CreateOptions();
        tabGroup.Bind(options, FindOptionIndex(options, (int)viewModel.CurrentTab.Value), index => viewModel.SetTab((StoreCategory)options[index].Id));
    }

    IReadOnlyList<UITabOption> CreateOptions()
    {
        if (primogemIcon == null && moraIcon == null && genesisCrystalIcon == null && starglitterStardustIcon == null)
        {
            return FallbackOptions;
        }

        return new[]
        {
            new UITabOption((int)StoreCategory.Primogem, "原石", primogemIcon),
            new UITabOption((int)StoreCategory.StarglitterStardust, "星尘星辉", starglitterStardustIcon),
            new UITabOption((int)StoreCategory.Mora, "摩拉", moraIcon),
            new UITabOption((int)StoreCategory.GenesisCrystal, "礼包", genesisCrystalIcon),
        };
    }

    static int FindOptionIndex(IReadOnlyList<UITabOption> options, int optionId)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].Id == optionId)
            {
                return i;
            }
        }

        return 0;
    }
}
