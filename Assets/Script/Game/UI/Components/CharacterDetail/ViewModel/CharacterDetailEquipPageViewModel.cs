using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;

public class CharacterDetailEquipPageViewModel 
{
    public readonly ReactiveProperty<EquipItemViewModel> currentWeapon = new();

    EquipItem selectedWeapon;
    EquipItem equippedWeapon;
    
    CharacterModel model;
    public readonly ReactiveCommand onReplaceClick = new();
    public readonly ReactiveCommand onEnhanceClick = new();
    CompositeDisposable disposable = new CompositeDisposable();
    public CharacterDetailEquipPageViewModel(CharacterModel model)
    {
        this.model = model;
        model.CurrentEquipRP
            .Subscribe(OnEquippedWeaponChanged)
            .AddTo(disposable);
    }
    
    public void SelectWeapon(EquipItem weapon)
    {
        selectedWeapon = weapon;
        currentWeapon.Value = weapon == null ? null : new EquipItemViewModel(weapon);
    }
    

    public bool HasPendingWeapon()
    {
        return selectedWeapon != null && selectedWeapon != equippedWeapon;
    }

    public void ConfirmChangeWeapon()
    {
        if (!HasPendingWeapon())
        {
            return;
        }

        model.ChangeEquip(selectedWeapon);
    }
    
    void OnEquippedWeaponChanged(EquipItem weapon)
    {
        equippedWeapon = weapon;

        if (!HasPendingWeapon())
        {
            ShowWeapon(equippedWeapon);
        }
    }    
    
    void ShowWeapon(EquipItem weapon)
    {
        if (weapon == null)
        {
            Debug.LogError("ShowWeapon failed: weapon is null.");
            return;
        }

        currentWeapon.Value = new EquipItemViewModel(weapon);
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
