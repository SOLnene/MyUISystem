using System;
using System.Collections.Generic;
using Game.Domain.Character;
using UniRx;
using UnityEngine;

public class CharacterSelectPanelView : SelectionPanelView
{
    [SerializeField]
    CharacterSlotView characterSlotPrefab;

    readonly List<CharacterSlotView> activeSlots = new();
    readonly Dictionary<CharacterModel, CharacterSlotView> slotsByCharacter = new();
    CharacterSelectionRequest selectionRequest;

    protected override void OnShow(object data)
    {
        ClearSlots();
        selectionRequest = data as CharacterSelectionRequest;
        if (selectionRequest == null)
        {
            Debug.LogError("CharacterSelectPanelView 参数错误");
            return;
        }

        foreach (CharacterModel character in selectionRequest.AvailableCharacters)
        {
            CharacterSlotView slot = Instantiate(characterSlotPrefab, content);
            bool isChecked = selectionRequest.MultiSelectionSet?.Contains(character) ?? false;
            bool isSelected = !selectionRequest.IsMultipleSelection
                && character == selectionRequest.InitialSingleSelectedCharacter;
            bool isSelectable = selectionRequest.CanSelectCharacter?.Invoke(character) ?? true;
            slot.Bind(character, isChecked, isSelectable, OnCharacterPicked);
            slot.SetSelected(isSelected);
            activeSlots.Add(slot);
            slotsByCharacter.Add(character, slot);
        }

        if (selectionRequest.IsMultipleSelection)
        {
            selectionRequest.MultiSelectionSet.OnDelta.Subscribe(delta =>
            {
                if (slotsByCharacter.TryGetValue(delta.Item, out CharacterSlotView slot))
                {
                    slot.SetChecked(delta.Added);
                }
            }).AddTo(disposable);
        }
    }

    protected override void OnHidden()
    {
        disposable.Clear();
        ClearSlots();
        selectionRequest = null;
    }

    protected override void OnCancelRequested()
    {
        selectionRequest?.OnCancelled?.Invoke();
    }

    void OnCharacterPicked(CharacterModel character)
    {
        if (selectionRequest?.IsMultipleSelection == true)
        {
            if (selectionRequest.MultiSelectionSet.Toggle(character)
                == LimitedSelectionResult.LimitReached)
            {
                selectionRequest.OnMultiSelectionLimitReached?.Invoke();
            }

            return;
        }

        selectionRequest?.OnSingleCharacterPicked?.Invoke(character);
        Hide();
    }

    void ClearSlots()
    {
        foreach (CharacterSlotView slot in activeSlots)
        {
            Destroy(slot.gameObject);
        }

        activeSlots.Clear();
        slotsByCharacter.Clear();
    }
}

public sealed class CharacterSelectionRequest
{
    public readonly IReadOnlyList<CharacterModel> AvailableCharacters;
    public readonly CharacterModel InitialSingleSelectedCharacter;
    public readonly LimitedSelectionSet<CharacterModel> MultiSelectionSet;
    public readonly Func<CharacterModel, bool> CanSelectCharacter;
    public readonly Action<CharacterModel> OnSingleCharacterPicked;
    public readonly Action OnCancelled;
    public readonly Action OnMultiSelectionLimitReached;

    public bool IsMultipleSelection => MultiSelectionSet != null;

    CharacterSelectionRequest(
        IReadOnlyList<CharacterModel> availableCharacters,
        CharacterModel initialSingleSelectedCharacter,
        LimitedSelectionSet<CharacterModel> multiSelectionSet,
        Func<CharacterModel, bool> canSelectCharacter,
        Action<CharacterModel> onSingleCharacterPicked,
        Action onCancelled,
        Action onMultiSelectionLimitReached)
    {
        AvailableCharacters = availableCharacters;
        InitialSingleSelectedCharacter = initialSingleSelectedCharacter;
        MultiSelectionSet = multiSelectionSet;
        CanSelectCharacter = canSelectCharacter;
        OnSingleCharacterPicked = onSingleCharacterPicked;
        OnCancelled = onCancelled;
        OnMultiSelectionLimitReached = onMultiSelectionLimitReached;
    }

    public static CharacterSelectionRequest ForSingle(
        IReadOnlyList<CharacterModel> availableCharacters,
        CharacterModel initialSelectedCharacter,
        Action<CharacterModel> onCharacterPicked,
        Action onCancelled = null,
        Func<CharacterModel, bool> canSelectCharacter = null)
    {
        return new CharacterSelectionRequest(
            availableCharacters,
            initialSelectedCharacter,
            null,
            canSelectCharacter,
            onCharacterPicked,
            onCancelled,
            null);
    }

    public static CharacterSelectionRequest ForMultiple(
        IReadOnlyList<CharacterModel> availableCharacters,
        LimitedSelectionSet<CharacterModel> selectionSet,
        Action onCancelled = null,
        Func<CharacterModel, bool> canSelectCharacter = null,
        Action onSelectionLimitReached = null)
    {
        return new CharacterSelectionRequest(
            availableCharacters,
            null,
            selectionSet,
            canSelectCharacter,
            null,
            onCancelled,
            onSelectionLimitReached);
    }
}
