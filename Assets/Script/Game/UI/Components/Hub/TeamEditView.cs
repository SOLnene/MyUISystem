using UnityEngine;
using UnityEngine.UI;

public class TeamEditView : UIView
{
    [SerializeField] private GameObject teamStagePrefab;
    [SerializeField] private Button backButton;
    [SerializeField] private Button[] characterButtons;
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private Button characterSelectCloseButton;

    private GameObject teamStageInstance;
    private Camera worldCamera;
    private bool worldCameraWasEnabled;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        worldCamera = Camera.main;
        worldCameraWasEnabled = worldCamera.enabled;
        worldCamera.enabled = false;

        teamStageInstance = Instantiate(teamStagePrefab);
        teamStageInstance.GetComponentInChildren<Camera>(true).depth = -10;
        characterSelectPanel.SetActive(false);
    }

    public override void OnAddListener()
    {
        base.OnAddListener();
        backButton.onClick.AddListener(OnBackClicked);
        characterSelectCloseButton.onClick.AddListener(CloseCharacterSelect);

        foreach (var characterButton in characterButtons)
        {
            characterButton.onClick.AddListener(OpenCharacterSelect);
        }
    }

    public override void OnRemoveListener()
    {
        backButton.onClick.RemoveListener(OnBackClicked);
        characterSelectCloseButton.onClick.RemoveListener(CloseCharacterSelect);

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
        characterSelectPanel.SetActive(true);
        characterSelectPanel.transform.SetAsLastSibling();
    }

    private void CloseCharacterSelect()
    {
        characterSelectPanel.SetActive(false);
    }

    private void OnBackClicked()
    {
        if (characterSelectPanel.activeSelf)
        {
            CloseCharacterSelect();
            return;
        }

        OnCancel();
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
