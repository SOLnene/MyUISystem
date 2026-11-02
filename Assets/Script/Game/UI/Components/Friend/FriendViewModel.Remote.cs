using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;

public partial class FriendViewModel
{
    internal readonly Subject<Unit> Changed = new();
    internal readonly Subject<string> ChatRequested = new();
    internal readonly ReactiveProperty<string> Status = new(string.Empty);
    internal readonly ReactiveProperty<bool> Busy = new(false);
    internal IReadOnlyList<FriendInfo> Requests { get; private set; } = Array.Empty<FriendInfo>();
    internal SocialPlayer SearchResult { get; private set; }
    internal readonly List<SocialMessage> Messages = new();
    internal string ChatUid { get; private set; }
    internal bool Sending { get; private set; }
    readonly CancellationTokenSource lifetime = new();
    readonly Dictionary<string, SocialSend> pending = new();
    CloudSaveHttpClient client = CloudSaveHttpClient.Default;
    readonly string owner = AccountSession.Current.PlayerId;
    bool disposed, reading;
    int chatVersion;
    long readCursor;
    internal FriendViewModel(CloudSaveHttpClient transport) : this(Array.Empty<FriendInfo>(), null) { client = transport; }
    void RequireOwner()
    {
        lifetime.Token.ThrowIfCancellationRequested();
        if (!AccountSession.Current.IsAuthorized || AccountSession.Current.PlayerId != owner)
            throw new InvalidOperationException("登录账号已变化，请重新打开好友页。");
    }
    internal async UniTask RefreshAsync()
    {
        if (disposed || Busy.Value) return;
        Busy.Value = true; Status.Value = "正在读取好友…";
        try
        {
            RequireOwner(); var data = await client.SocialAsync<SocialState>("friends", null, lifetime.Token); RequireOwner();
            Friends = data.friends.Select(p => p.ToFriend()).ToArray(); Requests = data.requests.Select(p => p.ToFriend(true)).ToArray();
            Status.Value = string.Empty; Changed.OnNext(Unit.Default);
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception error) { if (!disposed) Status.Value = Feedback(error); }
        finally { if (!disposed) Busy.Value = false; }
    }
    internal async UniTask SearchAsync(string uid)
    {
        if (disposed || Busy.Value) return;
        SearchResult = null;
        if (!long.TryParse(uid, out long number) || number < 100000001) { Status.Value = "请输入有效的玩家 UID。"; Changed.OnNext(Unit.Default); return; }
        await MutateAsync(async () => { SearchResult = await client.SocialAsync<SocialPlayer>("players/" + number, null, lifetime.Token); });
    }
    internal UniTask ApplyAsync() => SearchResult == null ? UniTask.CompletedTask : MutateAsync(async () =>
    {
        await client.SocialAsync<object>("friends/requests", new { uid = SearchResult.uid }, lifetime.Token);
        SearchResult.relationship = "sent"; Status.Value = "好友申请已发送。";
    });
    internal async UniTask RespondAsync(string uid, bool accept)
    {
        await MutateAsync(async () => { await client.SocialAsync<object>("friends/requests/" + uid + "/respond", new { accept }, lifetime.Token); });
        if (!disposed) await RefreshAsync();
    }
    async UniTask MutateAsync(Func<UniTask> action)
    {
        if (disposed || Busy.Value) return;
        Busy.Value = true; Status.Value = string.Empty;
        try { RequireOwner(); await action(); RequireOwner(); Changed.OnNext(Unit.Default); }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception error) { if (!disposed) Status.Value = Feedback(error); }
        finally { if (!disposed) Busy.Value = false; }
    }
    internal async UniTask OpenChatAsync(string uid)
    {
        ChatUid = uid; chatVersion++; readCursor = 0; Messages.Clear(); Changed.OnNext(Unit.Default); await ReadMessagesAsync();
    }
    internal void CloseChat() { ChatUid = null; chatVersion++; }
    internal async UniTask ReadMessagesAsync()
    {
        if (disposed || reading || Sending || ChatUid == null) return;
        int version = chatVersion; string peer = ChatUid; reading = true;
        try
        {
            RequireOwner(); long after = readCursor;
            var messages = await client.SocialAsync<SocialMessage[]>("friends/" + peer + "/messages?after=" + after, null, lifetime.Token);
            RequireOwner(); if (version != chatVersion) return;
            foreach (var message in messages) if (!Messages.Any(m => m.id == message.id)) Messages.Add(message);
            // Sending can finish while a read is in flight; only reads advance the history cursor.
            if (messages.Length > 0) readCursor = Math.Max(readCursor, messages[messages.Length - 1].id);
            Messages.Sort((a, b) => a.id.CompareTo(b.id));
            if (Messages.Count > 500) Messages.RemoveRange(0, Messages.Count - 500);
            if (messages.Length > 0) Changed.OnNext(Unit.Default);
        }
        catch (OperationCanceledException) when (disposed) { }
        catch (Exception error) { if (!disposed && version == chatVersion) Status.Value = Feedback(error); }
        finally { reading = false; }
    }
    internal async UniTask<bool> SendAsync(string text)
    {
        if (disposed || Sending || ChatUid == null || string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length > 1000) { Status.Value = "消息最多输入 1000 个字符。"; return false; }
        string peer = ChatUid; int version = chatVersion;
        // A retry keeps its original ID and body even if a previous response was lost.
        if (!pending.TryGetValue(peer, out var request)) pending[peer] = request = new SocialSend { text = text.Trim() };
        if (request.text != text.Trim()) { Status.Value = "上一条消息尚未确认，请保留原文重试。"; return false; }
        Sending = true;
        try
        {
            RequireOwner(); var message = await client.SocialAsync<SocialMessage>("friends/" + peer + "/messages", request, lifetime.Token); RequireOwner();
            pending.Remove(peer);
            if (version == chatVersion)
            {
                if (!Messages.Any(m => m.id == message.id)) Messages.Add(message);
                Messages.Sort((a, b) => a.id.CompareTo(b.id)); Status.Value = string.Empty; Changed.OnNext(Unit.Default);
            }
            return true;
        }
        catch (OperationCanceledException) when (disposed) { return false; }
        catch (Exception error) { if (!disposed) Status.Value = Feedback(error); return false; }
        finally { Sending = false; }
    }
    internal string PendingText(string peer) => pending.TryGetValue(peer, out var message) ? message.text : string.Empty;
    internal string Owner => owner;
    static string Feedback(Exception error) => error is CloudSaveServerException server ? server.Code switch
    {
        "player_not_found" => "没有找到该 UID 对应的玩家。", "cannot_add_self" => "不能添加自己为好友。",
        "incoming_request_exists" => "对方已发来申请，请在申请列表处理。", "friend_limit" => "好友数量已达到上限。",
        "friend_required" => "建立好友关系后才能聊天。", "request_not_found" => "该申请已不存在，请刷新列表。",
        "invalid_message" => "消息为空或超出长度限制。", "rate_limited" => "操作较频繁，请稍后重试。",
        _ => "连接好友服务失败，请重试；消息重试不会重复发送。"
    } : error.Message;
    void DisposeRemote()
    {
        disposed = true; lifetime.Cancel(); lifetime.Dispose(); Changed.Dispose(); ChatRequested.Dispose(); Status.Dispose(); Busy.Dispose();
    }
}
