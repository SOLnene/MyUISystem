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
            bool isChecked = selectParams.selection?.Contains(character) ?? false;
            bool isSelected = selectParams.selection == null && character == selectParams.selectedCharacter;
            bool isSelectable = selectParams.isSelectable?.Invoke(character) ?? true;
            slot.Bind(character, isChecked, isSelectable, OnCharacterPicked);
            slot.SetSelected(isSelected);
            activeSlots.Add(slot);
            slotsByCharacter.Add(character, slot);
        }

        if (selectParams.selection != null)
        {
            selectParams.selection.OnDelta.Subscribe(delta =>
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
        selectParams = null;
    }

    protected override void OnCancelRequested()
    {
        selectParams?.onCancel?.Invoke();
    }

    void OnCharacterPicked(CharacterModel character)
    {
        if (selectParams?.selection != null)
        {
            if (selectParams.selection.Toggle(character) == LimitedSelectionResult.LimitReached)
            {
                selectParams.onLimitReached?.Invoke();
            }

            return;
        }

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
        slotsByCharacter.Clear();
    }
}

public class CharacterSelectParams
{
    public IReadOnlyList<CharacterModel> candidates;
    public CharacterModel selectedCharacter;
    public Func<CharacterModel, bool> isSelectable;
    public Action<CharacterModel> onPicked;
    public Action onCancel;
    public LimitedSelectionSet<CharacterModel> selection;
    public Action onLimitReached;

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
