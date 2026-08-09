using System;

/// <summary>
/// 存档选择界面使用的档案元数据，独立保存在 profile.json 中，不包含实际游戏进度。
/// </summary>
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
