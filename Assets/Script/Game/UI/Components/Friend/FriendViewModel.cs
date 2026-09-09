using System;
using System.Collections.Generic;
using UniRx;

public class FriendViewModel : IDisposable
{
    public const int MaxFriendCount = 200;

    public IReadOnlyList<FriendInfo> Friends { get; }
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
    }

    public void Dispose()
    {
        SelectedTabIndex.Dispose();
    }
}
