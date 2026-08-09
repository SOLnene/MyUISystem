using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 管理 Profiles/{profileId} 目录及其 profile.json，完整游戏数据仍由 GameSaveSystem 读写。
/// </summary>
public sealed class SaveProfileManager
{
    const string ProfilesFolderName = "Profiles";
    const string ProfileFileName = "profile.json";
    const string SaveFileName = "save.json";
    const string BackupFileName = "save.backup.json";

    public static SaveProfileManager Instance { get; } = new SaveProfileManager();

    readonly string profilesRootPath;

    // 激活档案决定 GameSaveSystem 后续使用哪个目录，实际游戏数据在进入流程中加载。
    public SaveProfileInfo ActiveProfile { get; private set; }
    public string ActiveProfileDirectory => ActiveProfile == null
        ? string.Empty
        : GetProfileDirectory(ActiveProfile.profileId);

    SaveProfileManager()
    {
        profilesRootPath = Path.Combine(Application.persistentDataPath, ProfilesFolderName);
    }

    public List<SaveProfileInfo> LoadProfiles()
    {
        Directory.CreateDirectory(profilesRootPath);

        var profiles = new List<SaveProfileInfo>();
        foreach (string directory in Directory.EnumerateDirectories(profilesRootPath))
        {
            if (TryReadProfile(directory, out SaveProfileInfo profile))
            {
                profiles.Add(profile);
            }
        }

        if (profiles.Count == 0 && TryImportLegacyProfile(out SaveProfileInfo legacyProfile))
        {
            profiles.Add(legacyProfile);
        }

        // 时间均以 UTC 的 O 格式写入，可以直接按字符串倒序得到最近使用顺序。
        profiles.Sort(CompareProfiles);
        return profiles;
    }

    public SaveProfileInfo CreateProfile()
    {
        int profileNumber = LoadProfiles().Count + 1;
        string profileId = Guid.NewGuid().ToString("N");
        string profileDirectory = GetProfileDirectory(profileId);
        Directory.CreateDirectory(profileDirectory);

        string createdAtUtc = DateTime.UtcNow.ToString("O");
        var profile = new SaveProfileInfo(profileId, $"存档 {profileNumber}", createdAtUtc);
        WriteProfile(profileDirectory, profile);
        return profile;
    }

    public bool ActivateProfile(string profileId)
    {
        if (!TryGetProfile(profileId, out SaveProfileInfo profile, out _))
        {
            return false;
        }

        ActivateProfile(profile);
        return true;
    }

    internal bool TryGetProfile(
        string profileId,
        out SaveProfileInfo profile,
        out string profileDirectory)
    {
        // 此阶段只读取候选元数据，不改变当前激活档案。
        profileDirectory = string.Empty;
        if (!IsValidProfileId(profileId))
        {
            profile = null;
            return false;
        }

        profileDirectory = GetProfileDirectory(profileId);
        return TryReadProfile(profileDirectory, out profile);
    }

    internal void ActivateProfile(SaveProfileInfo profile)
    {
        // 游戏数据加载成功后才更新时间并提交激活状态。
        profile.lastPlayedAtUtc = DateTime.UtcNow.ToString("O");
        WriteProfile(GetProfileDirectory(profile.profileId), profile);
        ActiveProfile = profile;
    }

    static int CompareProfiles(SaveProfileInfo left, SaveProfileInfo right)
    {
        string leftTime = string.IsNullOrEmpty(left.lastPlayedAtUtc)
            ? left.createdAtUtc
            : left.lastPlayedAtUtc;
        string rightTime = string.IsNullOrEmpty(right.lastPlayedAtUtc)
            ? right.createdAtUtc
            : right.lastPlayedAtUtc;
        return string.CompareOrdinal(rightTime, leftTime);
    }

    bool TryImportLegacyProfile(out SaveProfileInfo profile)
    {
        // 保留根目录旧文件作为兜底，只复制到首个档案目录，不在迁移阶段删除原数据。
        profile = null;
        string legacySavePath = Path.Combine(Application.persistentDataPath, SaveFileName);
        string legacyBackupPath = Path.Combine(Application.persistentDataPath, BackupFileName);
        if (!File.Exists(legacySavePath) && !File.Exists(legacyBackupPath))
        {
            return false;
        }

        string profileId = Guid.NewGuid().ToString("N");
        string profileDirectory = GetProfileDirectory(profileId);
        try
        {
            Directory.CreateDirectory(profileDirectory);
            if (File.Exists(legacySavePath))
            {
                File.Copy(legacySavePath, Path.Combine(profileDirectory, SaveFileName));
            }

            if (File.Exists(legacyBackupPath))
            {
                File.Copy(legacyBackupPath, Path.Combine(profileDirectory, BackupFileName));
            }

            string sourcePath = File.Exists(legacySavePath) ? legacySavePath : legacyBackupPath;
            string createdAtUtc = File.GetLastWriteTimeUtc(sourcePath).ToString("O");
            profile = new SaveProfileInfo(profileId, "原存档", createdAtUtc);
            WriteProfile(profileDirectory, profile);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"导入旧存档失败: {exception.Message}");
            profile = null;
            return false;
        }
    }

    bool TryReadProfile(string profileDirectory, out SaveProfileInfo profile)
    {
        profile = null;
        string profilePath = Path.Combine(profileDirectory, ProfileFileName);
        if (!File.Exists(profilePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(profilePath, Encoding.UTF8);
            profile = JsonUtility.FromJson<SaveProfileInfo>(json);
            string directoryName = Path.GetFileName(profileDirectory);
            if (profile == null ||
                profile.profileId != directoryName ||
                !IsValidProfileId(profile.profileId) ||
                string.IsNullOrWhiteSpace(profile.displayName))
            {
                profile = null;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取存档档案失败: {exception.Message}");
            profile = null;
            return false;
        }
    }

    static void WriteProfile(string profileDirectory, SaveProfileInfo profile)
    {
        string profilePath = Path.Combine(profileDirectory, ProfileFileName);
        string json = JsonUtility.ToJson(profile, true);
        File.WriteAllText(profilePath, json, new UTF8Encoding(false));
    }

    string GetProfileDirectory(string profileId)
    {
        return Path.Combine(profilesRootPath, profileId);
    }

    static bool IsValidProfileId(string profileId)
    {
        return Guid.TryParseExact(profileId, "N", out _);
    }
}
