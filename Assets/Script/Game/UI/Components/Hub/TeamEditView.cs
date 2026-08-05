using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TeamEditView : UIView
{
    [SerializeField] private TeamStageView teamStagePrefab;
    [SerializeField] private Button backButton;
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private CharacterSelectPanelView characterSelectPanel;
    [SerializeField] private RectTransform middleArea;
    [SerializeField] private TMP_Text[] memberLabels;

    private TeamStageView teamStageInstance;
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
        foreach (var characterButton in characterButtons)
        {
            characterButton.onClick.AddListener(OpenCharacterSelect);
        }
    }

    public override void OnRemoveListener()
    {
        backButton.onClick.RemoveListener(OnBackClicked);
        foreach (var characterButton in characterButtons)
        {
            characterButton.onClick.RemoveListener(OpenCharacterSelect);
        }

        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        ReleaseTeamStage();
        base.OnClose();
    }

    public override void OnRelease()
    {
        ReleaseTeamStage();
        base.OnRelease();
    }

    private void OpenCharacterSelect()
    {
        characterSelectPanel.transform.SetAsLastSibling();
        characterSelectPanel.Show(new CharacterSelectParams(
            GameContext.Instance.CharacterRepository.Characters,
            null,
            null));
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

            var character = GameContext.Instance.CharacterRepository.GetByKey(
                teamStageInstance.GetMemberCharacterKey(i));
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
}
