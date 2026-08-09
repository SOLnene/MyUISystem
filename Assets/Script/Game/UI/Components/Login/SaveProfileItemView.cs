using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveProfileItemView : MonoBehaviour
{
    [SerializeField]
    Button button;

    [SerializeField]
    Image background;

    [SerializeField]
    TMP_Text nameLabel;

    [SerializeField]
    TMP_Text createdAtLabel;

    [SerializeField]
    Color normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    [SerializeField]
    Color selectedColor = new Color(0.55f, 0.75f, 0.95f, 1f);

    string profileId;
    Action<string> onSelected;

    void Reset()
    {
        button = GetComponent<Button>();
        background = GetComponent<Image>();
        nameLabel = transform.Find("NameLabel").GetComponent<TMP_Text>();
        createdAtLabel = transform.Find("CreatedAtLabel").GetComponent<TMP_Text>();
    }

    public void Bind(SaveProfileInfo profile, Action<string> selectedCallback)
    {
        profileId = profile.profileId;
        onSelected = selectedCallback;
        nameLabel.text = profile.displayName;
        createdAtLabel.text = GetActivityText(profile);
        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? selectedColor : normalColor;
    }

    static string GetActivityText(SaveProfileInfo profile)
    {
        if (DateTime.TryParse(profile.lastPlayedAtUtc, out DateTime lastPlayedAt))
        {
            return $"上次游玩：{lastPlayedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        if (!DateTime.TryParse(profile.createdAtUtc, out DateTime createdAt))
        {
            return $"新建存档：{profile.createdAtUtc}";
        }

        return $"新建存档：{createdAt.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    void HandleClick()
    {
        onSelected?.Invoke(profileId);
    }

    void OnDestroy()
    {
        button.onClick.RemoveListener(HandleClick);
    }
}
