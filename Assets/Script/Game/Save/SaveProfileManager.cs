using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class SaveProfileManager
{
    const string ProfilesFolderName = "Profiles";
    const string ProfileFileName = "profile.json";
    const string SaveFileName = "save.json";
    const string BackupFileName = "save.backup.json";

    public static SaveProfileManager Instance { get; } = new SaveProfileManager();

    readonly string profilesRootPath;

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
        if (!IsValidProfileId(profileId) ||
            !TryReadProfile(GetProfileDirectory(profileId), out SaveProfileInfo profile))
        {
            return false;
        }

        profile.lastPlayedAtUtc = DateTime.UtcNow.ToString("O");
        WriteProfile(GetProfileDirectory(profileId), profile);
        ActiveProfile = profile;
        return true;
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
