using System;
using Game.Domain.Character;
using TMPro;
using UniRx;
using UnityEngine;

public class CharacterSlotView : MonoBehaviour
{
    [SerializeField]
    SelectionSlotView selectionSlot;
    [SerializeField]
    TMP_Text levelText;

    readonly CompositeDisposable disposable = new();
    CharacterModel character;

    public void Bind(
        CharacterModel character,
        bool selected,
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
        selectionSlot.SetSelected(selected, true);
        selectionSlot.SetClickListener(() => onClick?.Invoke(this.character));
        
        character.LevelRP.Subscribe(level => levelText.text = $"Lv.{level}").AddTo(disposable);
    }

    public void SetSelected(bool selected)
    {
        selectionSlot.SetSelected(selected);
    }

    void OnDestroy()
    {
        disposable.Dispose();
    }
}
