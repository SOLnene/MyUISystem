using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Character;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.Events;
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
    [SerializeField] private Button quickFormationButton;
    [SerializeField] private Button previousTeamButton;
    [SerializeField] private Button nextTeamButton;
    [SerializeField] private UITabGroup teamPresetTabs;
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private CharacterSelectPanelView characterSelectPanel;
    [SerializeField] private TeamMemberLabelsView memberLabelsView;
    [SerializeField] private UITransitionGroup pageTransition;
    [SerializeField] private AnimatedPanel formationFooter;

    private TeamStageView teamStageInstance;
    private LimitedSelectionSet<CharacterModel> characterSelection;
    private readonly CompositeDisposable characterSelectionDisposable = new();
    private UnityAction[] characterButtonActions;
    private string[][] workingTeamPresets;
    private int editingPresetIndex;
    private int selectionMemberIndex = -1;
    private bool isReplacingMemberSelection;
    private Canvas uiCanvas;
    private Camera worldCamera;
    private bool worldCameraWasEnabled;
    private bool isClosing;
    private int transitionVersion;
    private CharacterModel focusedCandidate;
    private bool selectionAccepted;
    private bool selectionBusy;
    private bool returningToOverview;
    private int selectionVersion;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        isClosing = false;
        selectionBusy = returningToOverview = false;
        focusedCandidate = null;
        ++selectionVersion;
        ++transitionVersion;
        uiCanvas = GetComponent<Canvas>();
        worldCamera = Camera.main;
        worldCameraWasEnabled = worldCamera.enabled;
        worldCamera.enabled = false;

        if (teamStageInstance == null)
        {
            teamStageInstance = Instantiate(teamStagePrefab);
            DontDestroyOnLoad(teamStageInstance.gameObject);
        }

        teamStageInstance.Open();
        InitializeWorkingTeamPresets();
        editingPresetIndex = GameContext.Instance.TeamRepository.ActivePresetIndex;
        teamPresetTabs.Bind(TeamPresetOptions, editingPresetIndex, OnTeamPresetSelected);
        LoadTeamPreset(editingPresetIndex);

        characterSelectPanel.gameObject.SetActive(false);
        RefreshMemberLabels();
        Canvas.ForceUpdateCanvases();
        UpdateSlotPresentation();
        pageTransition.Show().Forget(Debug.LogException);
        memberLabelsView.ShowLabels().Forget(Debug.LogException);
    }

    private void LateUpdate()
    {
        UpdateSlotPresentation();
    }

    public override void OnAddListener()
    {
        base.OnAddListener();
        backButton.onClick.AddListener(OnBackClicked);
        confirmButton.onClick.AddListener(OnConfirmClicked);
        quickFormationButton.onClick.AddListener(BeginQuickFormation);
        previousTeamButton.onClick.AddListener(SelectPreviousTeamPreset);
        nextTeamButton.onClick.AddListener(SelectNextTeamPreset);
        characterButtonActions = new UnityAction[characterButtons.Length];
        for (int index = 0; index < characterButtons.Length; index++)
        {
            int memberIndex = index;
            UnityAction action = () => BeginNormalFormation(memberIndex);
            characterButtonActions[index] = action;
            characterButtons[index].onClick.AddListener(action);
        }
    }

    public override void OnRemoveListener()
    {
        backButton.onClick.RemoveListener(OnBackClicked);
        confirmButton.onClick.RemoveListener(OnConfirmClicked);
        quickFormationButton.onClick.RemoveListener(BeginQuickFormation);
        previousTeamButton.onClick.RemoveListener(SelectPreviousTeamPreset);
        nextTeamButton.onClick.RemoveListener(SelectNextTeamPreset);
        for (int index = 0; index < characterButtons.Length; index++)
        {
            characterButtons[index].onClick.RemoveListener(characterButtonActions[index]);
        }

        characterButtonActions = null;
        base.OnRemoveListener();
    }

    public override void OnCancel()
    {
        if (isClosing || selectionBusy || returningToOverview) return;
        if (characterSelectPanel.gameObject.activeSelf)
        {
            OnBackClicked();
            return;
        }

        isClosing = true;
        CloseWithTransition(++transitionVersion).Forget(Debug.LogException);
    }

    private async UniTask CloseWithTransition(int version)
    {
        await UniTask.WhenAll(pageTransition.Hide(), memberLabelsView.HideLabels());
        // A reopened or destroyed view must not be closed by an older transition.
        if (this != null && version == transitionVersion)
            base.OnCancel();
    }

    private void OnApplicationQuit()
    {
        isClosing = true;
        ++selectionVersion;
        ++transitionVersion;
    }

    public override void OnClose()
    {
        isClosing = true;
        ++selectionVersion;
        ++transitionVersion;
        pageTransition.Hide(true).Forget(Debug.LogException);
        memberLabelsView.HideLabelsImmediate();
        ReleaseCharacterSelection();
        CloseTeamStage();
        workingTeamPresets = null;
        base.OnClose();
    }

    public override void OnRelease()
    {
        isClosing = true;
        ++selectionVersion;
        ++transitionVersion;
        pageTransition.Hide(true).Forget(Debug.LogException);
        memberLabelsView.HideLabelsImmediate();
        ReleaseCharacterSelection();
        ReleaseTeamStage();
        workingTeamPresets = null;
        base.OnRelease();
    }

    private void BeginQuickFormation()
    {
        if (isClosing || returningToOverview || selectionBusy) return;
        BeginFormationSelection(-1, teamStageInstance.MemberCount);
    }

    private void BeginNormalFormation(int memberIndex)
    {
        if (isClosing || returningToOverview || selectionBusy) return;
        memberLabelsView.HideLabels().Forget(Debug.LogException);
        teamStageInstance.FocusMember(memberIndex);
        BeginFormationSelection(memberIndex, teamStageInstance.MemberCount);
    }

    private void BeginFormationSelection(int memberIndex, int maxSelectionCount)
    {
        ReleaseCharacterSelection();
        selectionMemberIndex = memberIndex;
        ++selectionVersion;
        selectionAccepted = false;
        characterSelection = new LimitedSelectionSet<CharacterModel>(maxSelectionCount);

        for (int index = 0; index < teamStageInstance.MemberCount; index++)
        {
            AddMemberToCharacterSelection(index);
        }

        characterSelection.OnDelta
            .Subscribe(OnCharacterSelectionChanged)
            .AddTo(characterSelectionDisposable);

        characterSelectPanel.transform.SetAsLastSibling();
        CharacterSelectionRequest request = CharacterSelectionRequest.ForMultiple(
            GameContext.Instance.CharacterRepository.Characters
                .Where(character => ModelViewer.Instance.CanShowCharacter(character.Definition.key)).ToArray(),
            characterSelection,
            OnSelectionCancelRequested);
        // Both formation modes reject the last removal before changing checkmarks or stage models.
        request.OnMultiCharacterPicked = OnFormationCharacterPicked;
        request.OnClosed = FinishCharacterSelection;
        focusedCandidate = memberIndex >= 0
            ? GameContext.Instance.CharacterRepository.GetByKey(teamStageInstance.GetMemberCharacterKey(memberIndex) ?? string.Empty)
            : null;
        request.FocusedCharacter = focusedCandidate;
        formationFooter.Hide().Forget(Debug.LogException);
        characterSelectPanel.Show(request);
        UpdateSelectionAction();
    }

    private void AddMemberToCharacterSelection(int memberIndex)
    {
        if (!teamStageInstance.TryGetMemberCharacterKey(memberIndex, out string characterKey))
        {
            return;
        }

        CharacterModel character = GameContext.Instance.CharacterRepository.GetByKey(
            characterKey);
        if (character != null)
        {
            characterSelection.TrySelect(character);
        }
    }

    private void InitializeWorkingTeamPresets()
    {
        TeamRepository teamRepository = GameContext.Instance.TeamRepository;
        workingTeamPresets = new string[TeamRepository.PresetCount][];
        for (int presetIndex = 0; presetIndex < TeamRepository.PresetCount; presetIndex++)
        {
            workingTeamPresets[presetIndex] = teamRepository.GetPresetSnapshot(presetIndex);
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

        RefreshMemberLabels();
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

        if (characterSelectPanel.gameObject.activeSelf || returningToOverview)
        {
            teamPresetTabs.Select(editingPresetIndex);
            return;
        }

        SaveCurrentStageToWorkingPreset();
        editingPresetIndex = presetIndex;
        LoadTeamPreset(editingPresetIndex);
    }

    private void OnBackClicked()
    {
        if (selectionBusy || returningToOverview) return;
        if (characterSelectPanel.gameObject.activeSelf)
        {
            OnSelectionCancelRequested();
            characterSelectPanel.Hide();
            return;
        }

        OnCancel();
    }

    private void OnConfirmClicked()
    {
        if (isClosing || returningToOverview || characterSelectPanel.gameObject.activeSelf) return;
        GameContext.Instance.TeamRepository.SetActivePreset(editingPresetIndex);
        GameSaveCoordinator.Instance.MarkDirty();
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

    private void OnFormationCharacterPicked(CharacterModel character)
    {
        if (characterSelection == null || selectionBusy || returningToOverview)
        {
            return;
        }

        if (selectionMemberIndex >= 0)
        {
            focusedCandidate = character;
            characterSelectPanel.SetFocusedCharacter(character);
            UpdateSelectionAction();
            teamStageInstance.PreviewMemberAsync(selectionMemberIndex, character.Definition.key)
                .Forget(Debug.LogException);
            return;
        }

        if (characterSelection.Contains(character))
        {
            int memberIndex = selectionMemberIndex >= 0
                ? selectionMemberIndex
                : FindMemberIndex(character.Definition.key);
            if (!HasOtherMember(memberIndex)) return;
            characterSelection.TryDeselect(character);
            return;
        }

        if (selectionMemberIndex < 0)
        {
            characterSelection.TrySelect(character);
            return;
        }

    }

    private void UpdateSelectionAction()
    {
        if (returningToOverview) return;
        if (selectionMemberIndex < 0)
        {
            characterSelectPanel.ConfigureSelectionAction("保存配置", ConfirmSelection, selectionBusy);
            return;
        }
        if (focusedCandidate == null || !CanSelectForCurrentMember(focusedCandidate))
        {
            characterSelectPanel.ConfigureSelectionAction(null, null);
            return;
        }
        string current = teamStageInstance.GetMemberCharacterKey(selectionMemberIndex);
        string label = current == focusedCandidate.Definition.key ? "换下"
            : string.IsNullOrEmpty(current) ? "加入" : "替换";
        characterSelectPanel.ConfigureSelectionAction(label, ConfirmSelection, selectionBusy);
    }

    private void ConfirmSelection()
    {
        if (selectionBusy || returningToOverview || isClosing) return;
        if (selectionMemberIndex < 0)
        {
            SaveQuickFormationAsync(selectionVersion).Forget(Debug.LogException);
            return;
        }
        if (focusedCandidate == null || !CanSelectForCurrentMember(focusedCandidate)) return;
        if (teamStageInstance.GetMemberCharacterKey(selectionMemberIndex) == focusedCandidate.Definition.key)
        {
            if (!HasOtherMember(selectionMemberIndex)) return;
            ReplaceSingleMemberSelection(null);
            teamStageInstance.ClearMember(selectionMemberIndex);
            SaveEditedPreset();
            selectionAccepted = returningToOverview = true;
            characterSelectPanel.Hide();
            return;
        }

        ReplaceSingleMemberSelection(focusedCandidate);
        ApplyNormalFormationSelectionAsync(focusedCandidate, characterSelection, selectionMemberIndex)
            .Forget(Debug.LogException);
    }

    private void SaveEditedPreset()
    {
        SaveCurrentStageToWorkingPreset();
        GameContext.Instance.TeamRepository.ReplacePreset(editingPresetIndex, workingTeamPresets[editingPresetIndex]);
        GameSaveCoordinator.Instance.MarkDirty();
    }

    private async UniTask SaveQuickFormationAsync(int version)
    {
        selectionBusy = true;
        UpdateSelectionAction();
        try
        {
            await UniTask.WaitUntil(() => !teamStageInstance.HasPendingLoads || isClosing,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (this == null || isClosing || version != selectionVersion) return;
        ReplaceSingleMemberSelection(null);
        SaveEditedPreset();
        selectionBusy = false;
        selectionAccepted = returningToOverview = true;
        characterSelectPanel.Hide();
    }

    private void OnSelectionCancelRequested()
    {
        if (selectionBusy || returningToOverview) return;
        selectionAccepted = false;
        returningToOverview = true;
    }

    private bool HasOtherMember(int excludedIndex)
    {
        for (int index = 0; index < teamStageInstance.MemberCount; index++)
        {
            if (index != excludedIndex && teamStageInstance.TryGetMemberCharacterKey(index, out _))
                return true;
        }
        return false;
    }

    private void ReplaceSingleMemberSelection(CharacterModel character)
    {
        // 更新勾选状态时屏蔽移除事件，旧模型由舞台在新资源就绪后交接。
        isReplacingMemberSelection = true;
        try
        {
            characterSelection.Clear();
            for (int index = 0; index < teamStageInstance.MemberCount; index++)
            {
                var member = index == selectionMemberIndex ? character
                    : GameContext.Instance.CharacterRepository.GetByKey(teamStageInstance.GetMemberCharacterKey(index) ?? string.Empty);
                if (member != null) characterSelection.TrySelect(member);
            }
        }
        finally
        {
            isReplacingMemberSelection = false;
        }
    }

    private async UniTask ApplyNormalFormationSelectionAsync(
        CharacterModel character,
        LimitedSelectionSet<CharacterModel> selection,
        int memberIndex)
    {
        int version = selectionVersion;
        selectionBusy = true;
        UpdateSelectionAction();
        TeamStageView stage = teamStageInstance;
        bool applied = false;
        try
        {
            await stage.SetMemberAsync(memberIndex, character.Definition.key);
            applied = stage.GetMemberCharacterKey(memberIndex) == character.Definition.key;
        }
        finally
        {
            // 关闭或后续点击已接管选择时，过期任务不得回滚当前勾选状态。
            if (characterSelection == selection && selection.Contains(character) && version == selectionVersion)
            {
                string actualKey = stage.GetMemberCharacterKey(memberIndex);
                if (actualKey != character.Definition.key)
                {
                    var actual = string.IsNullOrEmpty(actualKey)
                        ? null
                        : GameContext.Instance.CharacterRepository.GetByKey(actualKey);
                    ReplaceSingleMemberSelection(actual);
                    focusedCandidate = actual;
                    characterSelectPanel.SetFocusedCharacter(actual);
                    await stage.PreviewMemberAsync(memberIndex, actualKey);
                }
                RefreshMemberLabels();
                selectionBusy = false;
                if (!applied) UpdateSelectionAction();
            }
        }
        if (!applied || isClosing || version != selectionVersion) return;
        SaveEditedPreset();
        selectionAccepted = returningToOverview = true;
        characterSelectPanel.Hide();
    }

    private void OnCharacterSelectionChanged(SelectionDelta<CharacterModel> delta)
    {
        if (isReplacingMemberSelection)
        {
            return;
        }

        string characterKey = delta.Item.Definition.key;
        int memberIndex = selectionMemberIndex >= 0
            ? selectionMemberIndex
            : delta.Added
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

    private bool CanSelectForCurrentMember(CharacterModel character)
    {
        string characterKey = character.Definition.key;
        for (int memberIndex = 0; memberIndex < teamStageInstance.MemberCount; memberIndex++)
        {
            if (memberIndex != selectionMemberIndex
                && teamStageInstance.GetMemberCharacterKey(memberIndex) == characterKey)
            {
                return false;
            }
        }

        return true;
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
        memberLabelsView.Refresh(teamStageInstance);
    }

    private void UpdateSlotPresentation()
    {
        if (teamStageInstance == null)
        {
            return;
        }

        bool showEmptySlots = !isClosing && !returningToOverview && !characterSelectPanel.gameObject.activeSelf;
        for (int index = 0; index < characterButtons.Length; index++)
        {
            // The button's transparent hit area stays active for both occupied and empty slots.
            characterButtons[index].targetGraphic.enabled = showEmptySlots && index < teamStageInstance.MemberCount
                && !teamStageInstance.TryGetMemberCharacterKey(index, out _);
        }

        Camera eventCamera = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : uiCanvas.worldCamera;
        memberLabelsView.UpdatePositions(teamStageInstance, eventCamera);
    }

    private void ReleaseTeamStage()
    {
        if (teamStageInstance != null)
        {
            TeamStageView stage = teamStageInstance;
            teamStageInstance = null;
            stage.Release();
        }

        RestoreWorldCamera();
    }

    private void CloseTeamStage()
    {
        if (teamStageInstance != null)
        {
            teamStageInstance.Close();
        }

        RestoreWorldCamera();
    }

    private void RestoreWorldCamera()
    {
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

    private void FinishCharacterSelection()
    {
        if (isClosing || characterSelection == null) return;
        int memberIndex = selectionMemberIndex;
        string[] restoreKeys = memberIndex < 0 && !selectionAccepted
            ? (string[])workingTeamPresets[editingPresetIndex].Clone() : null;
        ReleaseCharacterSelection();
        selectionMemberIndex = -1;
        focusedCandidate = null;
        RestoreOverviewAsync(teamStageInstance, memberIndex, restoreKeys, ++selectionVersion)
            .Forget(Debug.LogException);
    }

    private async UniTask RestoreOverviewAsync(TeamStageView stage, int memberIndex, string[] restoreKeys, int version)
    {
        // Restore the committed actor before revealing the other slots; previews never become members on cancel.
        if (memberIndex >= 0)
            await stage.PreviewMemberAsync(memberIndex, stage.GetMemberCharacterKey(memberIndex));
        if (restoreKeys != null)
        {
            var tasks = new List<UniTask>();
            for (int index = 0; index < stage.MemberCount; index++)
            {
                if (string.IsNullOrEmpty(restoreKeys[index])) stage.ClearMember(index);
                else tasks.Add(stage.SetMemberAsync(index, restoreKeys[index]));
            }
            await UniTask.WhenAll(tasks);
        }
        if (this == null || isClosing || version != selectionVersion) return;
        stage.ShowOverview();
        RefreshMemberLabels();
        memberLabelsView.ShowLabels().Forget(Debug.LogException);
        formationFooter.Show().Forget(Debug.LogException);
        returningToOverview = false;
    }
}
