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
    [SerializeField]
    Sprite fateIcon;

    static readonly IReadOnlyList<UITabOption> FallbackOptions = new[]
    {
        new UITabOption((int)StoreCategory.Primogem, "原石"),
        new UITabOption((int)StoreCategory.Mora, "摩拉"),
        new UITabOption((int)StoreCategory.GenesisCrystal, "创世结晶"),
        new UITabOption((int)StoreCategory.Fate, "纠缠/相遇之缘"),
    };

    public void Bind(StoreViewModel viewModel)
    {
        tabGroup.Bind(CreateOptions(), (int)viewModel.CurrentTab.Value, index => viewModel.SetTab((StoreCategory)index));
    }

    IReadOnlyList<UITabOption> CreateOptions()
    {
        if (primogemIcon == null && moraIcon == null && genesisCrystalIcon == null && fateIcon == null)
        {
            return FallbackOptions;
        }

        return new[]
        {
            new UITabOption((int)StoreCategory.Primogem, "原石", primogemIcon),
            new UITabOption((int)StoreCategory.Mora, "摩拉", moraIcon),
            new UITabOption((int)StoreCategory.GenesisCrystal, "创世结晶", genesisCrystalIcon),
            new UITabOption((int)StoreCategory.Fate, "纠缠/相遇之缘", fateIcon),
        };
    }
}
