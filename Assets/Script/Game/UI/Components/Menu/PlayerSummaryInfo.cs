using UnityEngine;

public sealed class PlayerSummaryInfo
{
    public string DisplayName { get; }
    public string PlayerId { get; }
    public Sprite Avatar { get; }

    public PlayerSummaryInfo(string displayName, string playerId, Sprite avatar = null)
    {
        DisplayName = displayName;
        PlayerId = playerId;
        Avatar = avatar;
    }
}
