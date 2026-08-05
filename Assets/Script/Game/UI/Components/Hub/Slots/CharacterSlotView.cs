using System;
using Game.Domain.Character;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSlotView : MonoBehaviour
{
    [SerializeField]
    SelectionSlotView selectionSlot;
    [SerializeField]
    TMP_Text levelText;
    [SerializeField]
    Image checkedImage;

    readonly CompositeDisposable disposable = new();
    CharacterModel character;

    public void Bind(
        CharacterModel character,
        bool isChecked,
        bool interactable,
        Action<CharacterModel> onClick)
    {
        disposable.Clear();
        this.character = character;

        selectionSlot.ResetState();
        selectionSlot.BindVisual(
            CharacterVisualAddressResolver.ResolveIcon(character.Definition.key),
            RarityConfig.GetColor(character.Definition.rarity - 1));
        selectionSlot.SetInteractable(interactable);
        SetChecked(isChecked);
        selectionSlot.SetClickListener(() => onClick?.Invoke(this.character));
        
        character.LevelRP.Subscribe(level => levelText.text = $"Lv.{level}").AddTo(disposable);
    }

    public void SetSelected(bool selected)
    {
        selectionSlot.SetSelected(selected);
    }

    public void SetChecked(bool isChecked)
    {
        checkedImage.gameObject.SetActive(isChecked);
    }

    void OnDestroy()
    {
        disposable.Dispose();
    }
}
