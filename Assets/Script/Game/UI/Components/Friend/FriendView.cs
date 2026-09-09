using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class FriendView : UIView
{
    enum FriendPage
    {
        MyFriends,
        AddFriend,
        RecentPlayers
    }

    static readonly UITabOption[] TabOptions =
    {
        new((int)FriendPage.MyFriends, string.Empty),
        new((int)FriendPage.AddFriend, string.Empty),
        new((int)FriendPage.RecentPlayers, string.Empty)
    };

    [SerializeField]
    Button closeButton;
    [SerializeField]
    UITabGroup tabGroup;
    [SerializeField]
    TMP_Text titleText;
    [SerializeField]
    TMP_Text friendCountText;
    [SerializeField]
    GameObject addFriendPage;
    [SerializeField]
    GameObject recentPlayersPage;
    [SerializeField]
    FriendListView friendListView;

    readonly CompositeDisposable viewBindings = new();

    FriendViewModel viewModel;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        ClearViewState();

        viewModel = (FriendViewModel)data;
        closeButton.onClick.AddListener(OnCancel);
        tabGroup.Bind(TabOptions, viewModel.SelectedTabIndex);
        viewModel.SelectedTabIndex
            .Subscribe(RefreshSelectedPage)
            .AddTo(viewBindings);

        friendCountText.text = $"我的好友 {viewModel.Friends.Count}/{FriendViewModel.MaxFriendCount}";
        friendListView.Bind(viewModel.Friends, viewModel.RequestChat);
    }

    public override void OnClose()
    {
        ClearViewState();
        base.OnClose();
    }

    public override void OnRelease()
    {
        ClearViewState();
        viewBindings.Dispose();
        base.OnRelease();
    }

    void RefreshSelectedPage(int selectedIndex)
    {
        FriendPage selectedPage = (FriendPage)selectedIndex;
        friendListView.gameObject.SetActive(selectedPage == FriendPage.MyFriends);
        addFriendPage.SetActive(selectedPage == FriendPage.AddFriend);
        recentPlayersPage.SetActive(selectedPage == FriendPage.RecentPlayers);

        titleText.text = selectedPage switch
        {
            FriendPage.MyFriends => "好友 / 我的好友",
            FriendPage.AddFriend => "好友 / 添加好友",
            FriendPage.RecentPlayers => "好友 / 最近联机",
            _ => "好友"
        };
    }

    void ClearViewState()
    {
        closeButton.onClick.RemoveListener(OnCancel);
        viewBindings.Clear();
        friendListView.Clear();

        // FriendView 接管传入 VM 的生命周期，避免页面反复打开后遗留响应式订阅。
        viewModel?.Dispose();
        viewModel = null;
    }

}
