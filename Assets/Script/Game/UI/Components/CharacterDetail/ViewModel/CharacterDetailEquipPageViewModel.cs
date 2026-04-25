using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class CharacterDetailEquipPageViewModel 
{
    public readonly ReactiveProperty<EquipItemViewModel> currentWeapon = new();

    public readonly ReactiveCommand onReplaceClick = new();
    public readonly ReactiveCommand onEnhanceClick = new();

    public CharacterDetailEquipPageViewModel(EquipItem weapon)
    {
        currentWeapon.Value = new EquipItemViewModel(weapon);
    }

    public void Dispose()
    {
    }
}
