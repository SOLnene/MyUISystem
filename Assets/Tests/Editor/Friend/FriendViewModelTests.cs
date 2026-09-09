using NUnit.Framework;

public class FriendViewModelTests
{
    [Test]
    public void Constructor_KeepsFriendsAndSelectsFirstTab()
    {
        var friends = new[]
        {
            new FriendInfo(
                new PlayerSummaryInfo("测试好友", "100000001"),
                60,
                "在线",
                "用于验证好友列表。")
        };
        var viewModel = new FriendViewModel(friends, null);

        Assert.AreSame(friends, viewModel.Friends);
        Assert.AreEqual(0, viewModel.SelectedTabIndex.Value);

        viewModel.Dispose();
    }

    [Test]
    public void RequestChat_ForwardsRequestedPlayerId()
    {
        string requestedPlayerId = null;
        var viewModel = new FriendViewModel(
            new FriendInfo[0],
            playerId => requestedPlayerId = playerId);

        viewModel.RequestChat("100000007");

        Assert.AreEqual("100000007", requestedPlayerId);
        viewModel.Dispose();
    }
}
