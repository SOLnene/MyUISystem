using System;

[Serializable]
public sealed class SaveProfileInfo
{
    public string profileId;
    public string displayName;
    public string createdAtUtc;
    public string lastPlayedAtUtc;

    public SaveProfileInfo()
    {
    }

    public SaveProfileInfo(string profileId, string displayName, string createdAtUtc)
    {
        this.profileId = profileId;
        this.displayName = displayName;
        this.createdAtUtc = createdAtUtc;
        lastPlayedAtUtc = string.Empty;
    }
}
