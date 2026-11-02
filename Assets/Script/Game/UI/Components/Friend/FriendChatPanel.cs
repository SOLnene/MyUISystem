using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public sealed class FriendChatPanel : MonoBehaviour
{
    [SerializeField] AnimatedPanel panel;
    [SerializeField] AnimatedPanel backdropPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_InputField messageInput;
    [SerializeField] Button sendButton;
    [SerializeField] Button closeButton;
    [SerializeField] Button backdropButton;
    [SerializeField] ScrollRect scroll;
    [SerializeField] RectTransform content;
    [SerializeField] FriendMessageView messagePrefab;
    readonly List<FriendMessageView> rows = new();
    readonly CompositeDisposable subscriptions = new();
    CancellationTokenSource polling;
    FriendViewModel model;
    string peerName, peerUid;
    int renderedCount = -1;
    long lastMessage;
    bool closing;
    int presentationVersion;

    void Awake()
    {
        sendButton.onClick.AddListener(() => SendAsync().Forget());
        messageInput.onSubmit.AddListener(_ => SendAsync().Forget());
        closeButton.onClick.AddListener(Close); backdropButton.onClick.AddListener(Close);
    }
    internal void Open(FriendViewModel viewModel, FriendInfo peer)
    {
        Release(); ++presentationVersion; closing = false; model = viewModel; peerUid = peer.Player.PlayerId; peerName = peer.Player.DisplayName;
        titleText.text = peerName + "\n<size=18>UID " + peerUid + "</size>";
        messageInput.text = model.PendingText(peerUid); renderedCount = -1; lastMessage = 0; sendButton.interactable = true;
        model.Changed.Subscribe(_ => Render()).AddTo(subscriptions);
        polling = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        gameObject.SetActive(true);
        backdropPanel.Show().Forget(); panel.Show().Forget();
        BeginAsync(model, peerUid, polling.Token).Forget();
    }
    async UniTask BeginAsync(FriendViewModel active, string uid, CancellationToken token)
    {
        try
        {
            await active.OpenChatAsync(uid);
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(3), ignoreTimeScale: true, cancellationToken: token);
                await active.ReadMessagesAsync();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    void Render()
    {
        if (model == null || model.ChatUid != peerUid) return;
        var messages = model.Messages; long last = messages.Count == 0 ? 0 : messages[messages.Count - 1].id;
        if (renderedCount == messages.Count && lastMessage == last) return;
        bool follow = renderedCount <= 0 || scroll.verticalNormalizedPosition < .1f;
        while (rows.Count < messages.Count) rows.Add(Instantiate(messagePrefab, content));
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].gameObject.SetActive(i < messages.Count);
            if (i < messages.Count) rows[i].Bind(messages[i], messages[i].senderUid == model.Owner, peerName);
        }
        emptyText.gameObject.SetActive(messages.Count == 0); emptyText.text = "还没有消息，打个招呼吧。";
        renderedCount = messages.Count; lastMessage = last;
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (follow) scroll.verticalNormalizedPosition = 0;
    }
    async UniTask SendAsync()
    {
        var active = model; string uid = peerUid;
        if (active == null || active.Sending) return;
        sendButton.interactable = false;
        bool sent = await active.SendAsync(messageInput.text);
        if (active != model || !gameObject.activeInHierarchy || uid != peerUid) return;
        if (sent) { messageInput.text = string.Empty; scroll.verticalNormalizedPosition = 0; }
        sendButton.interactable = true;
    }
    void Close()
    {
        if (closing) return;
        closing = true; Release(); HideAsync().Forget();
    }
    async UniTask HideAsync()
    {
        int version = ++presentationVersion;
        await UniTask.WhenAll(panel.Hide(), backdropPanel.Hide());
        if (this != null && version == presentationVersion) gameObject.SetActive(false);
    }
    internal void HideImmediate()
    {
        ++presentationVersion; Release();
        panel.HideImmediate(); backdropPanel.HideImmediate(); gameObject.SetActive(false);
    }
    void Release()
    {
        // Stop this window's polling before changing the conversation owner or releasing subscriptions.
        polling?.Cancel(); polling?.Dispose(); polling = null;
        subscriptions.Clear(); model?.CloseChat(); model = null;
    }
    void OnDestroy() { ++presentationVersion; Release(); subscriptions.Dispose(); }
}
