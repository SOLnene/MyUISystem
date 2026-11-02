using System;
using System.Collections.Generic;
using UniRx;

public partial class FriendViewModel : IDisposable
{
    public const int MaxFriendCount = 200;

    public IReadOnlyList<FriendInfo> Friends { get; private set; }
    public ReactiveProperty<int> SelectedTabIndex { get; } = new(0);

    readonly Action<string> onChatRequested;

    public FriendViewModel(
        IReadOnlyList<FriendInfo> friends,
        Action<string> onChatRequested)
    {
        Friends = friends;
        this.onChatRequested = onChatRequested;
    }

    public void RequestChat(string playerId)
    {
        onChatRequested?.Invoke(playerId);
        ChatRequested.OnNext(playerId);
    }

    public void Dispose()
    {
        DisposeRemote();
        SelectedTabIndex.Dispose();
    }
}
