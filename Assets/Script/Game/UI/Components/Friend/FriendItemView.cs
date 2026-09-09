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

    string playerId;
    Action<string> onChatRequested;
    Sprite defaultAvatar;

    void Awake()
    {
        defaultAvatar = avatarIcon.sprite;
        chatButton.onClick.AddListener(HandleChatClicked);
    }

    public void Bind(FriendInfo friend, Action<string> chatRequested)
    {
        playerId = friend.Player.PlayerId;
        onChatRequested = chatRequested;
        playerNameText.text = friend.Player.DisplayName;
        levelText.text = $"Lv.{friend.Level}";
        presenceText.text = friend.PresenceText;
        signatureText.text = friend.Signature;

        // 远端资料未提供头像时，继续显示 Prefab 中配置的默认头像。
        avatarIcon.sprite = friend.Player.Avatar != null
            ? friend.Player.Avatar
            : defaultAvatar;
    }

    public void Unbind()
    {
        playerId = null;
        onChatRequested = null;
    }

    void HandleChatClicked()
    {
        onChatRequested?.Invoke(playerId);
    }

    void OnDestroy()
    {
        chatButton.onClick.RemoveListener(HandleChatClicked);
    }
}
