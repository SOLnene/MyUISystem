using System.Collections.Generic;
using UnityEngine;

public class StoreTabView : MonoBehaviour
{
    [SerializeField]
    UITabGroup tabGroup;
    [SerializeField]
    Sprite goldIcon;
    [SerializeField]
    Sprite fateIcon;
    [SerializeField]
    Sprite item203Icon;

    static readonly IReadOnlyList<UITabOption> FallbackOptions = new[]
    {
        new UITabOption((int)StoreTabType.Gold, "金币"),
        new UITabOption((int)StoreTabType.Fate, "纠缠/相遇"),
        new UITabOption((int)StoreTabType.Item203, "203"),
    };

    public void Bind(StoreViewModel viewModel)
    {
        tabGroup.Bind(CreateOptions(), (int)viewModel.CurrentTab.Value, index => viewModel.SetTab((StoreTabType)index));
    }

    IReadOnlyList<UITabOption> CreateOptions()
    {
        if (goldIcon == null && fateIcon == null && item203Icon == null)
        {
            return FallbackOptions;
        }

        return new[]
        {
            new UITabOption((int)StoreTabType.Gold, "金币", goldIcon),
            new UITabOption((int)StoreTabType.Fate, "纠缠/相遇", fateIcon),
            new UITabOption((int)StoreTabType.Item203, "203", item203Icon),
        };
    }
}
