using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using Unity.VisualScripting;
using UnityEngine;

public class CharacterDetailEquipPageViewModel 
{
    public readonly ReactiveProperty<EquipItemViewModel> currentWeapon = new();

    EquipItem selectedWeapon;
    EquipItem equippedWeapon;
    
    CharacterModel model;
    public readonly ReactiveCommand onReplaceClick = new();
    public readonly ReactiveCommand onEnhanceClick = new();
    
    bool isSelectingWeapon;

    public readonly ReactiveProperty<bool> replaceButtonInteractable = new(true);
    
    CompositeDisposable disposable = new CompositeDisposable();
    public CharacterDetailEquipPageViewModel(CharacterModel model)
    {
        this.model = model;
        model.CurrentEquipRP
            .Subscribe(OnEquippedWeaponChanged)
            .AddTo(disposable);
    }
    
   
    

    public bool HasPreviewWeapon()
    {
        return selectedWeapon != null && selectedWeapon != equippedWeapon;
    }
    
    public void BeginSelectWeapon()
    {
        isSelectingWeapon = true;
        selectedWeapon = null;
        ShowWeapon(equippedWeapon);
        RefreshReplaceButtonState();
    }
    
    public void SelectWeapon(EquipItem weapon)
    {
        selectedWeapon = weapon;
        currentWeapon.Value = weapon == null ? null : new EquipItemViewModel(weapon);
        RefreshReplaceButtonState();
    }
    
    //确认更换武器
    public void ConfirmChangeWeapon()
    {
        if (!HasPreviewWeapon())
        {
            return;
        }

        model.ChangeEquip(selectedWeapon);
        RefreshReplaceButtonState();
    }
    
    //当前角色实际装备武器变化时调用
    void OnEquippedWeaponChanged(EquipItem weapon)
    {
        equippedWeapon = weapon;

        if (!HasPreviewWeapon())
        {
            ShowWeapon(equippedWeapon);
        }
        RefreshReplaceButtonState();
    }    
    
    //切换预览的武器
    void ShowWeapon(EquipItem weapon)
    {
        if (weapon == null)
        {
            Debug.LogError("ShowWeapon failed: weapon is null.");
            return;
        }

        currentWeapon.Value = new EquipItemViewModel(weapon);
    }

    //关闭武器选择界面时调用
    public void CancelSelect()
    {
        isSelectingWeapon = false;
        selectedWeapon = null;
        ShowWeapon(equippedWeapon);
        RefreshReplaceButtonState();
    }
    
    void RefreshReplaceButtonState()
    {
        replaceButtonInteractable.Value = !isSelectingWeapon || HasPreviewWeapon();
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
