using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class MaterialItemViewModel : ItemViewModel
{
    MaterialItem Material => Model as MaterialItem;

    public readonly ReactiveProperty<int> count = new();
    public readonly ReactiveProperty<int> neededCount = new();
    public readonly ReactiveProperty<bool> isEnough = new();

    public MaterialItemViewModel(MaterialItem item) : base(item)
    {
        count.Value = item.Count;
        neededCount.Value = item.NeededCount;

        UpdateState();
    }

    void UpdateState()
    {
        isEnough.Value = count.Value >= neededCount.Value;

        displayCountText.Value = $"{count.Value}/{neededCount.Value}";
    }

    public override void Refresh()
    {
        count.Value = Material.Count;

        UpdateState();
    }
}
