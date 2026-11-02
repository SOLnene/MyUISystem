using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendItemView : MonoBehaviour
{
    [SerializeField]
    Image avatarIcon;
    [SerializeField]
    TMP_Text playerNameText;
    [SerializeField]
    TMP_Text levelText;
    [SerializeField]
    TMP_Text presenceText;
    [SerializeField]
    TMP_Text signatureText;
    [SerializeField]
    Button chatButton;
    [SerializeField] Button rejectButton;
    [SerializeField] TMP_Text actionText;
    [SerializeField] AnimatedPanel itemPanel;
    internal AnimatedPanel Panel => itemPanel;
    Action<string> onRejectRequested;

    string playerId;
    Action<string> onChatRequested;
    // Lists can bind while inactive, before Awake has run.
    [SerializeField] Sprite defaultAvatar;

    void Awake()
    {
        chatButton.onClick.AddListener(HandleChatClicked);
        rejectButton.onClick.AddListener(HandleRejectClicked);
    }

    public void Bind(FriendInfo friend, Action<string> chatRequested)
    {
        playerId = friend.Player.PlayerId;
        onChatRequested = chatRequested;
        playerNameText.text = friend.Player.DisplayName;
        levelText.text = friend.Level > 0 ? $"Lv.{friend.Level}" : "UID " + friend.Player.PlayerId;
        presenceText.text = friend.PresenceText;
        signatureText.text = friend.Signature;
        actionText.text = friend.IsRequest ? "接受" : "聊天";
        rejectButton.gameObject.SetActive(friend.IsRequest);

        // 远端资料未提供头像时，继续显示 Prefab 中配置的默认头像。
        avatarIcon.sprite = friend.Player.Avatar != null
            ? friend.Player.Avatar
            : defaultAvatar;
    }

    public void Unbind()
    {
        playerId = null;
        onChatRequested = null;
        onRejectRequested = null;
    }

    void HandleChatClicked()
    {
        onChatRequested?.Invoke(playerId);
    }

    internal void BindActions(FriendInfo friend, Action<string> action, Action<string> reject)
    { Bind(friend, action); onRejectRequested = reject; }
    void HandleRejectClicked() => onRejectRequested?.Invoke(playerId);

    void OnDestroy()
    {
        chatButton.onClick.RemoveListener(HandleChatClicked);
        rejectButton.onClick.RemoveListener(HandleRejectClicked);
    }
}
