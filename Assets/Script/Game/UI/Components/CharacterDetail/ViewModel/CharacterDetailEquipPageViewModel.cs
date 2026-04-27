using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;

public class CharacterDetailEquipPageViewModel 
{
    public readonly ReactiveProperty<EquipItemViewModel> currentWeapon = new();
    
    //选中的切换武器
    public readonly ReactiveProperty<EquipItem> pendingWeapon = new();
    CharacterModel model;
    public readonly ReactiveCommand onReplaceClick = new();
    public readonly ReactiveCommand onEnhanceClick = new();

    CompositeDisposable disposable = new CompositeDisposable();
    public CharacterDetailEquipPageViewModel(CharacterModel model)
    {
        this.model = model;
        model.CurrentEquipRP
            .Subscribe(weapon =>
            {
                currentWeapon.Value = weapon == null ? null : new EquipItemViewModel(weapon);
            })
            .AddTo(disposable);
    }

    public void SetPendingWeapon(EquipItem equipItem)
    {
        pendingWeapon.Value = equipItem;
    }

    public bool HasPendingWeapon()
    {
        return pendingWeapon.Value != null;
    }

    public void ConfirmChangeWeapon()
    {
        if (pendingWeapon.Value == null)
        {
            return;
        }

        model.ChangeEquip(pendingWeapon.Value);
        pendingWeapon.Value = null;
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
