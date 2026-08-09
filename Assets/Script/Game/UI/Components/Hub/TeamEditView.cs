using System.Collections.Generic;
using Game.Domain.Character;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class TeamEditView : UIView
{
    private static readonly UITabOption[] TeamPresetOptions =
    {
        new UITabOption(0, string.Empty),
        new UITabOption(1, string.Empty),
        new UITabOption(2, string.Empty),
        new UITabOption(3, string.Empty)
    };

    [SerializeField] private TeamStageView teamStagePrefab;
    [SerializeField] private Button backButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button previousTeamButton;
    [SerializeField] private Button nextTeamButton;
    [SerializeField] private UITabGroup teamPresetTabs;
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private CharacterSelectPanelView characterSelectPanel;
    [SerializeField] private RectTransform middleArea;
    [SerializeField] private TMP_Text[] memberLabels;

    private TeamStageView teamStageInstance;
    private LimitedSelectionSet<CharacterModel> characterSelection;
    private readonly CompositeDisposable characterSelectionDisposable = new();
    private string[][] workingTeamPresets;
    private int editingPresetIndex;
    private Canvas uiCanvas;
    private Camera worldCamera;
    private bool worldCameraWasEnabled;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        uiCanvas = GetComponent<Canvas>();
        worldCamera = Camera.main;
        worldCameraWasEnabled = worldCamera.enabled;
        worldCamera.enabled = false;

        teamStageInstance = Instantiate(teamStagePrefab);
        teamStageInstance.DisplayCamera.depth = -10;
        InitializeWorkingTeamPresets();
        editingPresetIndex = GameContext.Instance.TeamRepository.ActivePresetIndex;
        teamPresetTabs.Bind(TeamPresetOptions, editingPresetIndex, OnTeamPresetSelected);
        LoadTeamPreset(editingPresetIndex);

        characterSelectPanel.gameObject.SetActive(false);
        RefreshMemberLabels();
        Canvas.ForceUpdateCanvases();
        UpdateMemberLabelPositions();
    }

    private void LateUpdate()
    {
        UpdateMemberLabelPositions();
    }

    public override void OnAddListener()
    {
        base.OnAddListener();
        backButton.onClick.AddListener(OnBackClicked);
        confirmButton.onClick.AddListener(OnConfirmClicked);
        previousTeamButton.onClick.AddListener(SelectPreviousTeamPreset);
        nextTeamButton.onClick.AddListener(SelectNextTeamPreset);
        foreach (var characterButton in characterButtons)
        {
            characterButton.onClick.AddListener(OpenCharacterSelect);
        }
    }

    public override void OnRemoveListener()
    {
        backButton.onClick.RemoveListener(OnBackClicked);
        confirmButton.onClick.RemoveListener(OnConfirmClicked);
        previousTeamButton.onClick.RemoveListener(SelectPreviousTeamPreset);
        nextTeamButton.onClick.RemoveListener(SelectNextTeamPreset);
        foreach (var characterButton in characterButtons)
        {
            characterButton.onClick.RemoveListener(OpenCharacterSelect);
        }

        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        ReleaseCharacterSelection();
        ReleaseTeamStage();
        workingTeamPresets = null;
        base.OnClose();
    }

    public override void OnRelease()
    {
        ReleaseCharacterSelection();
        ReleaseTeamStage();
        workingTeamPresets = null;
        base.OnRelease();
    }

    private void OpenCharacterSelect()
    {
        characterSelectPanel.transform.SetAsLastSibling();
        characterSelectPanel.Show(new CharacterSelectParams(
            GameContext.Instance.CharacterRepository.Characters,
            null,
            null)
        {
            selection = characterSelection
        });
    }

    private void InitializeWorkingTeamPresets()
    {
        TeamRepository teamRepository = GameContext.Instance.TeamRepository;
        workingTeamPresets = new string[TeamRepository.PresetCount][];
        for (int presetIndex = 0; presetIndex < TeamRepository.PresetCount; presetIndex++)
        {
            workingTeamPresets[presetIndex] = teamRepository.GetPresetSnapshot(presetIndex);
        }

        if (teamRepository.HasAnyInitializedPreset)
        {
            return;
        }

        string[] initialPreset = workingTeamPresets[teamRepository.ActivePresetIndex];
        int memberCount = Mathf.Min(teamStageInstance.MemberCount, initialPreset.Length);
        for (int memberIndex = 0; memberIndex < memberCount; memberIndex++)
        {
            if (teamStageInstance.TryGetMemberCharacterKey(memberIndex, out string characterKey))
            {
                initialPreset[memberIndex] = characterKey;
            }
        }
    }

    private void LoadTeamPreset(int presetIndex)
    {
        ReleaseCharacterSelection();
        string[] memberKeys = workingTeamPresets[presetIndex];
        for (int memberIndex = 0; memberIndex < teamStageInstance.MemberCount; memberIndex++)
        {
            string characterKey = memberIndex < memberKeys.Length
                ? memberKeys[memberIndex]
                : null;
            if (string.IsNullOrEmpty(characterKey))
            {
                teamStageInstance.ClearMember(memberIndex);
                continue;
            }

            teamStageInstance.SetMemberAsync(memberIndex, characterKey)
                .Forget(Debug.LogException);
        }

        InitializeCharacterSelection(memberKeys);
        RefreshMemberLabels();
    }

    private void InitializeCharacterSelection(IReadOnlyList<string> memberKeys)
    {
        characterSelection = new LimitedSelectionSet<CharacterModel>(teamStageInstance.MemberCount);
        for (int memberIndex = 0; memberIndex < memberKeys.Count; memberIndex++)
        {
            string characterKey = memberKeys[memberIndex];
            if (string.IsNullOrEmpty(characterKey))
            {
                continue;
            }

            CharacterModel character = GameContext.Instance.CharacterRepository.GetByKey(
                characterKey);
            if (character != null)
            {
                characterSelection.TrySelect(character);
            }
        }

        characterSelection.OnDelta
            .Subscribe(OnCharacterSelectionChanged)
            .AddTo(characterSelectionDisposable);
    }

    private void SaveCurrentStageToWorkingPreset()
    {
        string[] memberKeys = workingTeamPresets[editingPresetIndex];
        for (int memberIndex = 0; memberIndex < memberKeys.Length; memberIndex++)
        {
            memberKeys[memberIndex] = memberIndex < teamStageInstance.MemberCount
                && teamStageInstance.TryGetMemberCharacterKey(
                    memberIndex,
                    out string characterKey)
                ? characterKey
                : null;
        }
    }

    private void OnTeamPresetSelected(int presetIndex)
    {
        if (presetIndex == editingPresetIndex)
        {
            return;
        }

        SaveCurrentStageToWorkingPreset();
        editingPresetIndex = presetIndex;
        LoadTeamPreset(editingPresetIndex);
    }

    private void OnBackClicked()
    {
        if (characterSelectPanel.gameObject.activeSelf)
        {
            characterSelectPanel.Hide();
            return;
        }

        OnCancel();
    }

    private void OnConfirmClicked()
    {
        SaveCurrentStageToWorkingPreset();
        GameContext.Instance.TeamRepository.ReplaceAllPresets(
            workingTeamPresets,
            editingPresetIndex);
        GameSaveCoordinator.Instance.MarkDirty();
        OnCancel();
    }

    private void SelectPreviousTeamPreset()
    {
        SelectTeamPreset(-1);
    }

    private void SelectNextTeamPreset()
    {
        SelectTeamPreset(1);
    }

    private void SelectTeamPreset(int offset)
    {
        int currentIndex = teamPresetTabs.SelectedIndex;
        if (currentIndex < 0)
        {
            return;
        }

        int nextIndex = (currentIndex + offset + TeamPresetOptions.Length)
            % TeamPresetOptions.Length;
        teamPresetTabs.Select(nextIndex);
    }

    private void OnCharacterSelectionChanged(SelectionDelta<CharacterModel> delta)
    {
        string characterKey = delta.Item.Definition.key;
        int memberIndex = delta.Added
            ? FindEmptyMemberIndex()
            : FindMemberIndex(characterKey);
        if (memberIndex < 0)
        {
            return;
        }

        if (delta.Added)
        {
            teamStageInstance.SetMemberAsync(memberIndex, characterKey)
                .Forget(Debug.LogException);
        }
        else
        {
            teamStageInstance.ClearMember(memberIndex);
        }

        RefreshMemberLabels();
    }

    private int FindEmptyMemberIndex()
    {
        for (int index = 0; index < teamStageInstance.MemberCount; index++)
        {
            if (!teamStageInstance.TryGetMemberCharacterKey(index, out _))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindMemberIndex(string characterKey)
    {
        for (int index = 0; index < teamStageInstance.MemberCount; index++)
        {
            if (teamStageInstance.GetMemberCharacterKey(index) == characterKey)
            {
                return index;
            }
        }

        return -1;
    }

    private void RefreshMemberLabels()
    {
        int memberCount = Mathf.Min(teamStageInstance.MemberCount, memberLabels.Length);
        for (int i = 0; i < memberLabels.Length; i++)
        {
            if (i >= memberCount)
            {
                memberLabels[i].gameObject.SetActive(false);
                continue;
            }

            if (!teamStageInstance.TryGetMemberCharacterKey(i, out string characterKey))
            {
                memberLabels[i].gameObject.SetActive(false);
                continue;
            }

            var character = GameContext.Instance.CharacterRepository.GetByKey(characterKey);
            memberLabels[i].gameObject.SetActive(character != null);
            if (character != null)
            {
                memberLabels[i].text = $"{character.Name.Value}\nLv.{character.LevelRP.Value}";
            }
        }
    }

    private void UpdateMemberLabelPositions()
    {
        if (teamStageInstance == null)
        {
            return;
        }

        Camera eventCamera = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : uiCanvas.worldCamera;
        int memberCount = Mathf.Min(teamStageInstance.MemberCount, memberLabels.Length);
        for (int i = 0; i < memberCount; i++)
        {
            TMP_Text memberLabel = memberLabels[i];
            if (!memberLabel.gameObject.activeSelf)
            {
                continue;
            }

            Vector3 screenPosition = teamStageInstance.DisplayCamera.WorldToScreenPoint(
                teamStageInstance.GetMemberInfoPosition(i));
            if (screenPosition.z <= 0f)
            {
                continue;
            }

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    middleArea,
                    screenPosition,
                    eventCamera,
                    out Vector3 worldPosition))
            {
                memberLabel.rectTransform.position = worldPosition;
            }
        }
    }

    private void ReleaseTeamStage()
    {
        if (teamStageInstance == null)
        {
            return;
        }

        Destroy(teamStageInstance);
        teamStageInstance = null;

        if (worldCamera != null)
        {
            worldCamera.enabled = worldCameraWasEnabled;
            worldCamera = null;
        }
    }

    private void ReleaseCharacterSelection()
    {
        characterSelectionDisposable.Clear();
        characterSelection?.Dispose();
        characterSelection = null;
    }
}
