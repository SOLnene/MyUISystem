using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;


public partial class LoginView : UIView
{
    [SerializeField]
    RectTransform saveListContent;

    [SerializeField]
    SaveProfileItemView saveItemTemplate;

    [SerializeField]
    Button createButton;

    [SerializeField]
    Button enterButton;

    readonly Dictionary<string, SaveProfileItemView> profileItems = new();

    LoginViewModel viewModel;

    void Reset()
    {
        saveListContent = transform.Find("PageRoot/SaveListScroll/Viewport/SaveListContent") as RectTransform;
        saveItemTemplate = saveListContent.GetComponentInChildren<SaveProfileItemView>(true);
        createButton = transform.Find("PageRoot/CreateButton").GetComponent<Button>();
        enterButton = transform.Find("PageRoot/EnterButton").GetComponent<Button>();
    }

    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
        viewModel = new LoginViewModel(SaveProfileManager.Instance);
        saveItemTemplate.gameObject.SetActive(false);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        RebuildProfileItems();
    }

    public override void OnAddListener()
    {
        base.OnAddListener();
        createButton.onClick.AddListener(CreateProfile);
        enterButton.onClick.AddListener(ConfirmProfile);
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
        createButton.onClick.RemoveListener(CreateProfile);
        enterButton.onClick.RemoveListener(ConfirmProfile);
    }

    public override void OnClose()
    {
        base.OnClose();
    }

    public override void OnRelease()
    {
        base.OnRelease();
        profileItems.Clear();
    }

    void RebuildProfileItems()
    {
        foreach (SaveProfileItemView item in profileItems.Values)
        {
            Destroy(item.gameObject);
        }

        profileItems.Clear();
        viewModel.LoadProfiles();
        foreach (SaveProfileInfo profile in viewModel.Profiles)
        {
            AddProfileItem(profile);
        }

        RefreshSelection(null, viewModel.SelectedProfile?.profileId);
    }

    void CreateProfile()
    {
        string previousProfileId = viewModel.SelectedProfile?.profileId;
        SaveProfileInfo profile = viewModel.CreateProfile();
        AddProfileItem(profile);
        RefreshSelection(previousProfileId, profile.profileId);
    }

    void SelectProfile(string profileId)
    {
        string previousProfileId = viewModel.SelectedProfile?.profileId;
        if (viewModel.SelectProfile(profileId))
        {
            RefreshSelection(previousProfileId, profileId);
        }
    }

    void ConfirmProfile()
    {
        if (viewModel.ConfirmSelection())
        {
            Debug.Log($"已选择存档: {viewModel.SelectedProfile.displayName}");
            EnterSelectedProfileAsync().Forget();
        }
    }

    async UniTask EnterSelectedProfileAsync()
    {
        createButton.interactable = false;
        enterButton.interactable = false;

        if (await FlowManager.Instance.EnterSelectedProfileAsync())
        {
            return;
        }

        createButton.interactable = true;
        enterButton.interactable = viewModel.SelectedProfile != null;
    }

    void AddProfileItem(SaveProfileInfo profile)
    {
        SaveProfileItemView item = Instantiate(saveItemTemplate, saveListContent);
        item.name = $"SaveProfileItem_{profile.profileId}";
        item.gameObject.SetActive(true);
        item.Bind(profile, SelectProfile);
        profileItems.Add(profile.profileId, item);
    }

    void RefreshSelection(string previousProfileId, string selectedProfileId)
    {
        if (!string.IsNullOrEmpty(previousProfileId) &&
            profileItems.TryGetValue(previousProfileId, out SaveProfileItemView previousItem))
        {
            previousItem.SetSelected(false);
        }

        if (!string.IsNullOrEmpty(selectedProfileId) &&
            profileItems.TryGetValue(selectedProfileId, out SaveProfileItemView selectedItem))
        {
            selectedItem.SetSelected(true);
        }

        enterButton.interactable = !string.IsNullOrEmpty(selectedProfileId);
    }
}
