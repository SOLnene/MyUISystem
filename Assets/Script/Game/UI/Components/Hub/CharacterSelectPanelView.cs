using System;
using System.Collections.Generic;
using Game.Domain.Character;
using UnityEngine;

public class CharacterSelectPanelView : SelectionPanelView
{
    [SerializeField]
    CharacterSlotView characterSlotPrefab;

    readonly List<CharacterSlotView> activeSlots = new();
    CharacterSelectParams selectParams;

    protected override void OnShow(object data)
    {
        ClearSlots();
        selectParams = data as CharacterSelectParams;
        if (selectParams == null)
        {
            Debug.LogError("CharacterSelectPanelView 参数错误");
            return;
        }

        foreach (CharacterModel character in selectParams.candidates)
        {
            CharacterSlotView slot = Instantiate(characterSlotPrefab, content);
            bool isSelected = character == selectParams.selectedCharacter;
            bool isSelectable = selectParams.isSelectable?.Invoke(character) ?? true;
            slot.Bind(character, isSelected, isSelectable, OnCharacterPicked);
            activeSlots.Add(slot);
        }
    }

    protected override void OnHidden()
    {
        ClearSlots();
        selectParams = null;
    }

    protected override void OnCancelRequested()
    {
        selectParams?.onCancel?.Invoke();
    }

    void OnCharacterPicked(CharacterModel character)
    {
        selectParams?.onPicked?.Invoke(character);
        Hide();
    }

    void ClearSlots()
    {
        foreach (CharacterSlotView slot in activeSlots)
        {
            Destroy(slot.gameObject);
        }

        activeSlots.Clear();
    }
}

public class CharacterSelectParams
{
    public IReadOnlyList<CharacterModel> candidates;
    public CharacterModel selectedCharacter;
    public Func<CharacterModel, bool> isSelectable;
    public Action<CharacterModel> onPicked;
    public Action onCancel;

    public CharacterSelectParams(
        IReadOnlyList<CharacterModel> candidates,
        CharacterModel selectedCharacter,
        Action<CharacterModel> onPicked,
        Action onCancel = null,
        Func<CharacterModel, bool> isSelectable = null)
    {
        this.candidates = candidates;
        this.selectedCharacter = selectedCharacter;
        this.isSelectable = isSelectable;
        this.onPicked = onPicked;
        this.onCancel = onCancel;
    }
}
