using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

internal sealed class SettingsViewModel : IDisposable
{
    internal enum Page { Graphics, Audio }

    // Display labels map to explicit modes; saved data never depends on parsing UI text.
    internal static readonly IReadOnlyList<string> WindowModeChoices = Array.AsReadOnly(new[]
    {
        "窗口",
        "无边框窗口",
        "独占全屏"
    });
    readonly List<Vector2Int> resolutions;
    internal IReadOnlyList<string> ResolutionChoices { get; }

    internal ReactiveProperty<Page> SelectedPage { get; } = new(Page.Audio);
    internal ReactiveProperty<int> MasterVolume { get; } = new(10);
    internal ReactiveProperty<int> UIVolume { get; } = new(10);
    internal ReactiveProperty<bool> Muted { get; } = new(false);
    internal ReactiveProperty<int> WindowMode { get; } = new(0);
    internal ReactiveProperty<int> Resolution { get; } = new(2);

    internal SettingsViewModel()
    {
        Vector2Int savedResolution = SettingsPreferences.Resolution;
        resolutions = Screen.resolutions.Select(value => new Vector2Int(value.width, value.height))
            .Append(savedResolution).Distinct().OrderBy(value => value.x).ThenBy(value => value.y).ToList();
        ResolutionChoices = resolutions.Select(value => $"{value.x}×{value.y}").ToArray();
        MasterVolume.Value = SettingsPreferences.MasterVolume;
        UIVolume.Value = SettingsPreferences.UIVolume;
        Muted.Value = SettingsPreferences.Muted;
        WindowMode.Value = SettingsPreferences.WindowMode switch
        {
            FullScreenMode.Windowed => 0,
            FullScreenMode.ExclusiveFullScreen => 2,
            _ => 1
        };
        Resolution.Value = resolutions.IndexOf(savedResolution);
    }

    internal void SetMasterVolume(float value)
    {
        MasterVolume.Value = Mathf.Clamp(Mathf.RoundToInt(value), 0, 10);
        SettingsPreferences.SaveAudio(MasterVolume.Value, UIVolume.Value, Muted.Value);
    }

    internal void SetUIVolume(float value)
    {
        UIVolume.Value = Mathf.Clamp(Mathf.RoundToInt(value), 0, 10);
        SettingsPreferences.SaveAudio(MasterVolume.Value, UIVolume.Value, Muted.Value);
    }

    internal void SetMuted(bool muted)
    {
        Muted.Value = muted;
        SettingsPreferences.SaveAudio(MasterVolume.Value, UIVolume.Value, Muted.Value);
    }

    internal void SetWindowMode(int index)
    {
        WindowMode.Value = Mathf.Clamp(index, 0, WindowModeChoices.Count - 1);
        SaveDisplay();
    }

    internal void SetResolution(int index)
    {
        Resolution.Value = Mathf.Clamp(index, 0, ResolutionChoices.Count - 1);
        SaveDisplay();
    }

    void SaveDisplay()
    {
        FullScreenMode mode = WindowMode.Value switch
        {
            0 => FullScreenMode.Windowed,
            2 => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.FullScreenWindow
        };
        SettingsPreferences.SaveDisplay(resolutions[Resolution.Value], mode);
    }

    internal void RestoreCurrentPage()
    {
        if (SelectedPage.Value == Page.Audio)
        {
            MasterVolume.Value = 10;
            UIVolume.Value = 10;
            Muted.Value = false;
            SettingsPreferences.SaveAudio(MasterVolume.Value, UIVolume.Value, Muted.Value);
        }
        else
        {
            WindowMode.Value = 0;
            int defaultResolution = resolutions.IndexOf(new Vector2Int(1920, 1080));
            Resolution.Value = defaultResolution >= 0 ? defaultResolution : resolutions.Count - 1;
            SaveDisplay();
        }
    }

    public void Dispose()
    {
        SelectedPage.Dispose();
        MasterVolume.Dispose();
        UIVolume.Dispose();
        Muted.Dispose();
        WindowMode.Dispose();
        Resolution.Dispose();
    }
}
