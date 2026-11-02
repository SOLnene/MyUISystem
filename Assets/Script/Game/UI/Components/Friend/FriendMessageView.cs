using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FriendMessageView : MonoBehaviour
{
    [SerializeField] TMP_Text senderText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] Image bubble;
    [SerializeField] VerticalLayoutGroup alignment;

    internal void Bind(SocialMessage message, bool own, string peerName)
    {
        string time = DateTimeOffset.TryParse(message.sentAtUtc, out var sent) ? sent.ToLocalTime().ToString("HH:mm") : string.Empty;
        senderText.text = (own ? "我" : peerName) + "  " + time;
        bodyText.richText = false; bodyText.text = message.text;
        alignment.padding = own ? new RectOffset(120, 0, 0, 0) : new RectOffset(0, 120, 0, 0);
        bubble.color = own ? new Color(.31f, .40f, .51f, 1) : new Color(.91f, .89f, .84f, 1);
        bodyText.color = senderText.color = own ? new Color(.98f, .96f, .91f) : new Color(.22f, .26f, .32f);
    }
}
