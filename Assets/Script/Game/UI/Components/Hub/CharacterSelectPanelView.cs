using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectPanelView : SelectionPanelView
{
    [SerializeField]
    CharacterSlotView characterSlotPrefab;
    [SerializeField]
    AnimatedPanelGroup slotAnimations;
    [SerializeField] Button selectionActionButton;
    [SerializeField] TMP_Text selectionActionLabel;

    readonly List<CharacterSlotView> activeSlots = new();
    readonly List<AnimatedPanel> slotPanels = new();
    readonly Dictionary<CharacterModel, CharacterSlotView> slotsByCharacter = new();
    CharacterSelectionRequest selectionRequest;
    Action selectionAction;

    void Awake()
    {
        selectionActionButton.onClick.AddListener(() => selectionAction?.Invoke());
    }

    internal void ConfigureSelectionAction(string label, Action action, bool busy = false)
    {
        selectionAction = action;
        selectionActionButton.gameObject.SetActive(action != null);
        selectionActionButton.interactable = !busy;
        if (clickHandler != null) clickHandler.interactable = !busy;
        if (action != null) selectionActionLabel.text = label;
    }

    internal void SetFocusedCharacter(CharacterModel character)
    {
        selectionRequest.FocusedCharacter = character;
        foreach (var slot in slotsByCharacter)
            slot.Value.SetSelected(slot.Key == character);
    }

    protected override void OnShow(object data)
    {
        ClearSlots();
        ConfigureSelectionAction(null, null);
        selectionRequest = data as CharacterSelectionRequest;
        if (selectionRequest == null)
        {
            Debug.LogError("CharacterSelectPanelView 参数错误");
            return;
        }

        BuildSlotsAsync(selectionRequest, slotCreateVersion).Forget(Debug.LogException);
    }

    async UniTask BuildSlotsAsync(CharacterSelectionRequest request, int version)
    {
        var token = this.GetCancellationTokenOnDestroy();
        var iconLoads = new List<UniTask<Sprite>>(request.AvailableCharacters.Count);
        foreach (var character in request.AvailableCharacters)
        {
            iconLoads.Add(ResourceManager.Instance.LoadAssetAsync<Sprite>(
                CharacterVisualAddressResolver.ResolveAvatarIcon(character.Definition.key), token));
        }
        try
        {
            await UniTask.WhenAll(iconLoads);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        // Bind from the warmed cache so cards enter with portraits, never empty Image quads.
        if (this == null || version != slotCreateVersion || !gameObject.activeInHierarchy) return;

        foreach (CharacterModel character in request.AvailableCharacters)
        {
            CharacterSlotView slot = Instantiate(characterSlotPrefab, content);
            bool isChecked = selectionRequest.MultiSelectionSet?.Contains(character) ?? false;
            bool isSelected = character == request.FocusedCharacter;
            bool isSelectable = selectionRequest.CanSelectCharacter?.Invoke(character) ?? true;
            slot.Bind(character, isChecked, isSelectable, OnCharacterPicked);
            slot.SetSelected(isSelected);
            activeSlots.Add(slot);
            slotPanels.Add(slot.GetComponentInChildren<AnimatedPanel>(true));
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

        slotAnimations.Show(slotPanels).Forget(Debug.LogException);
    }

    protected override void OnBeforeHide()
    {
        // Stop delayed entries while the parent panel fades out, preserving visible cards.
        slotAnimations.ReleasePanels(slotPanels);
        selectionAction = null;
    }

    protected override void OnHidden()
    {
        var onClosed = selectionRequest?.OnClosed;
        disposable.Clear();
        ClearSlots();
        selectionRequest = null;
        ConfigureSelectionAction(null, null);
        onClosed?.Invoke();
    }

    protected override void OnCancelRequested()
    {
        selectionRequest?.OnCancelled?.Invoke();
    }

    void OnCharacterPicked(CharacterModel character)
    {
        if (selectionRequest?.IsMultipleSelection == true)
        {
            if (selectionRequest.OnMultiCharacterPicked != null)
            {
                selectionRequest.OnMultiCharacterPicked(character);
                return;
            }
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
        slotAnimations.ReleasePanels(slotPanels);
        slotPanels.Clear();
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

    internal Action<CharacterModel> OnMultiCharacterPicked { get; set; }
    internal Action OnClosed { get; set; }
    internal CharacterModel FocusedCharacter { get; set; }

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
        FocusedCharacter = initialSingleSelectedCharacter;
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
