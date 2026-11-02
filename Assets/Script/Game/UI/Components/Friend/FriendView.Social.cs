using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public partial class FriendView
{
    [SerializeField] AnimatedPanel pagePanel;
    [SerializeField] AnimatedPanel contentPanel;
    [SerializeField] AnimatedPanel topPanel;
    [SerializeField] AnimatedPanel footerPanel;
    [SerializeField] AnimatedPanel backgroundPanel;
    [SerializeField] RawImage renderTextureBackground;
    [SerializeField] AspectRatioFitter renderTextureAspect;
    [SerializeField] GameObject inputBlocker;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text requestEmptyText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_InputField uidInput;
    [SerializeField] Button searchButton;
    [SerializeField] Button applyButton;
    [SerializeField] Button refreshButton;
    [SerializeField] TMP_Text searchResultText;
    [SerializeField] FriendListView requestListView;
    [SerializeField] FriendChatPanel chatPanel;
    IReadOnlyList<FriendInfo> renderedFriends, renderedRequests;
    bool closing, switching, panelsShown;
    int presentationVersion;

    void BeginSocialOpen()
    {
        ++presentationVersion;
        closing = switching = panelsShown = false; renderedFriends = renderedRequests = null;
        // An unassigned RT must reveal the prefab fallback instead of drawing a white RawImage.
        renderTextureBackground.enabled = renderTextureBackground.texture != null;
        if (renderTextureBackground.enabled)
            renderTextureAspect.aspectRatio = (float)renderTextureBackground.texture.width / renderTextureBackground.texture.height;
        chatPanel.HideImmediate();
    }
    void InitializeSocial()
    {
        searchButton.onClick.AddListener(Search); applyButton.onClick.AddListener(Apply); refreshButton.onClick.AddListener(Refresh);
        viewModel.Changed.Subscribe(_ => RefreshData()).AddTo(viewBindings);
        viewModel.ChatRequested.Subscribe(OpenChat).AddTo(viewBindings);
        viewModel.Status.Subscribe(text => statusText.text = text).AddTo(viewBindings);
        viewModel.Busy.Subscribe(busy =>
        { searchButton.interactable = applyButton.interactable = refreshButton.interactable = !busy; }).AddTo(viewBindings);
        requestListView.RejectRequested = uid => viewModel.RespondAsync(uid, false).Forget();
        RefreshData(); ShowAsync().Forget(); viewModel.RefreshAsync().Forget();
    }
    void ReleaseSocial()
    {
        ++presentationVersion; panelsShown = false;
        chatPanel.HideImmediate(); requestListView.Clear();
        topPanel.HideImmediate(); footerPanel.HideImmediate(); backgroundPanel.HideImmediate(); contentPanel.HideImmediate();
        searchButton.onClick.RemoveListener(Search); applyButton.onClick.RemoveListener(Apply); refreshButton.onClick.RemoveListener(Refresh);
    }
    void Search() => viewModel.SearchAsync(uidInput.text.Trim()).Forget();
    void Apply() => viewModel.ApplyAsync().Forget();
    void Refresh() => viewModel.RefreshAsync().Forget();
    void OpenChat(string uid)
    {
        var friend = viewModel.Friends.FirstOrDefault(f => f.Player.PlayerId == uid);
        if (friend != null) chatPanel.Open(viewModel, friend);
    }
    void RefreshData()
    {
        bool friendsChanged = !ReferenceEquals(renderedFriends, viewModel.Friends);
        bool requestsChanged = !ReferenceEquals(renderedRequests, viewModel.Requests);
        friendCountText.text = $"我的好友 {viewModel.Friends.Count}/{FriendViewModel.MaxFriendCount}";
        if (friendsChanged)
        { friendListView.Bind(viewModel.Friends, viewModel.RequestChat); renderedFriends = viewModel.Friends; }
        if (requestsChanged)
        { requestListView.Bind(viewModel.Requests, uid => viewModel.RespondAsync(uid, true).Forget()); renderedRequests = viewModel.Requests; }
        emptyText.gameObject.SetActive(viewModel.Friends.Count == 0);
        emptyText.text = AccountSession.Current.IsAuthorized ? "还没有好友，前往「添加好友」搜索玩家 UID。" : "登录账号后可查看好友列表。";
        requestEmptyText.gameObject.SetActive(viewModel.Requests.Count == 0);
        var result = viewModel.SearchResult;
        searchResultText.richText = false;
        searchResultText.text = result == null ? "输入玩家 UID，搜索真实玩家。" : result.name + "\nUID " + result.uid + "\n" +
            (result.relationship == "friend" ? "已是好友" : result.relationship == "sent" ? "已发送申请" : result.relationship == "received" ? "对方已申请，请在下方处理" : result.relationship == "self" ? "这是你自己" : "可以发送好友申请");
        applyButton.gameObject.SetActive(result?.relationship == "none");
        if (panelsShown && !closing &&
            ((viewModel.SelectedTabIndex.Value == 0 && friendsChanged) || (viewModel.SelectedTabIndex.Value == 1 && requestsChanged)))
            ShowCurrentList();
    }
    async UniTask ShowAsync()
    {
        int version = presentationVersion;
        inputBlocker.SetActive(true);
        await pagePanel.Show(instant: true);
        await contentPanel.Show(instant: true);
        panelsShown = true;
        // Rows retain their own stagger and input gates; HTTP and off-screen rows do not extend the page-wide block.
        ShowCurrentList();
        await UniTask.WhenAll(topPanel.Show(), footerPanel.Show(), backgroundPanel.Show());
        if (this != null && version == presentationVersion && !closing && viewModel != null) inputBlocker.SetActive(false);
    }
    void ShowCurrentList()
    {
        if (viewModel.SelectedTabIndex.Value == 0) friendListView.ShowItems().Forget();
        else if (viewModel.SelectedTabIndex.Value == 1) requestListView.ShowItems().Forget();
    }
    void SwitchSocialPage(int index) => SwitchPageAsync(index).Forget();
    async UniTask SwitchPageAsync(int index)
    {
        if (switching || closing) return;
        if (renderedFriends == null) { RefreshSelectedPage(index); return; }
        switching = true; inputBlocker.SetActive(true); chatPanel.HideImmediate();
        await contentPanel.Hide(instant: true);
        if (closing || viewModel == null) return;
        RefreshSelectedPage(index); await contentPanel.Show(instant: true);
        ShowCurrentList();
        switching = false; if (!closing && viewModel != null) inputBlocker.SetActive(false);
    }
    public override void OnCancel()
    {
        if (closing) return;
        closing = true; ++presentationVersion; panelsShown = false; CloseAsync().Forget();
    }
    async UniTask CloseAsync()
    {
        int version = presentationVersion;
        inputBlocker.SetActive(true); chatPanel.HideImmediate();
        await UniTask.WhenAll(topPanel.Hide(), footerPanel.Hide(), backgroundPanel.Hide(), contentPanel.Hide());
        // A close cancelled by a reopen cannot hide the newer presentation.
        if (this == null || version != presentationVersion) return;
        await pagePanel.Hide(instant: true); base.OnCancel();
    }
}
