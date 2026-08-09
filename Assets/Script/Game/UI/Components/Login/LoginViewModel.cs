using System.Collections.Generic;

public sealed class LoginViewModel
{
    readonly SaveProfileManager profileManager;
    readonly List<SaveProfileInfo> profiles = new List<SaveProfileInfo>();

    public IReadOnlyList<SaveProfileInfo> Profiles => profiles;
    // SelectedProfile 只表示界面选择；确认后才会成为 SaveProfileManager.ActiveProfile。
    public SaveProfileInfo SelectedProfile { get; private set; }

    public LoginViewModel(SaveProfileManager profileManager)
    {
        this.profileManager = profileManager;
    }

    public void LoadProfiles()
    {
        profiles.Clear();
        profiles.AddRange(profileManager.LoadProfiles());
        SelectedProfile = profiles.Count > 0 ? profiles[0] : null;
    }

    public SaveProfileInfo CreateProfile()
    {
        SaveProfileInfo profile = profileManager.CreateProfile();
        profiles.Add(profile);
        SelectedProfile = profile;
        return profile;
    }

    public bool SelectProfile(string profileId)
    {
        foreach (SaveProfileInfo profile in profiles)
        {
            if (profile.profileId != profileId)
            {
                continue;
            }

            SelectedProfile = profile;
            return true;
        }

        return false;
    }

    public bool ConfirmSelection()
    {
        return SelectedProfile != null;
    }
}
