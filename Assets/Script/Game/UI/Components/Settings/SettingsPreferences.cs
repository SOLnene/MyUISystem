using UnityEngine;

// Device preferences stay separate from account credentials and gameplay saves.
internal static class SettingsPreferences
{
    const string Prefix = "MyUISystem.Settings.";

    internal static int MasterVolume => ReadVolume("MasterVolume");
    internal static int UIVolume => ReadVolume("UIVolume");
    internal static bool Muted => PlayerPrefs.GetInt(Prefix + "Muted", 0) != 0;
    internal static Vector2Int Resolution => new(
        Mathf.Max(1, PlayerPrefs.GetInt(Prefix + "Width", Screen.width)),
        Mathf.Max(1, PlayerPrefs.GetInt(Prefix + "Height", Screen.height)));

    internal static FullScreenMode WindowMode
    {
        get
        {
            var mode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "WindowMode", (int)Screen.fullScreenMode);
            return mode == FullScreenMode.Windowed || mode == FullScreenMode.ExclusiveFullScreen
                ? mode : FullScreenMode.FullScreenWindow;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyAtStartup()
    {
        ApplyAudio();
        if (PlayerPrefs.HasKey(Prefix + "Width") && PlayerPrefs.HasKey(Prefix + "Height"))
            ApplyDisplay(Resolution, WindowMode);
    }

    internal static void SaveAudio(int masterVolume, int uiVolume, bool muted)
    {
        PlayerPrefs.SetFloat(Prefix + "MasterVolume", Mathf.Clamp01(masterVolume / 10f));
        PlayerPrefs.SetFloat(Prefix + "UIVolume", Mathf.Clamp01(uiVolume / 10f));
        PlayerPrefs.SetInt(Prefix + "Muted", muted ? 1 : 0);
        ApplyAudio();
        PlayerPrefs.Save();
    }

    internal static void SaveDisplay(Vector2Int resolution, FullScreenMode mode)
    {
        PlayerPrefs.SetInt(Prefix + "Width", resolution.x);
        PlayerPrefs.SetInt(Prefix + "Height", resolution.y);
        PlayerPrefs.SetInt(Prefix + "WindowMode", (int)mode);
        ApplyDisplay(resolution, mode);
        PlayerPrefs.Save();
    }

    static int ReadVolume(string key) => Mathf.RoundToInt(Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, 1f)) * 10f);

    static void ApplyAudio()
    {
        AudioManager.Instance.SetBusVolume(AudioBus.Master, Muted ? 0f : MasterVolume / 10f);
        AudioManager.Instance.SetBusVolume(AudioBus.UI, UIVolume / 10f);
    }

    static void ApplyDisplay(Vector2Int resolution, FullScreenMode mode)
    {
        // The Editor's Game view is a preview surface, not the player's operating-system window.
#if !UNITY_EDITOR
        Screen.SetResolution(resolution.x, resolution.y, mode);
#endif
    }
}
