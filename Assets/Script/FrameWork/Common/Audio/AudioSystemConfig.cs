using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 音频系统的全局装配配置，集中保存 Catalog、Mixer 路由和 Source 池容量。
/// </summary>
[CreateAssetMenu(fileName = "AudioSystemConfig", menuName = "Game/Audio/Audio System Config")]
public sealed class AudioSystemConfig : ScriptableObject
{
    [SerializeField]
    AudioCatalog catalog;
    [SerializeField]
    AudioMixer mixer;
    [SerializeField]
    AudioMixerGroup masterOutput;
    [SerializeField]
    AudioMixerGroup bgmOutput;
    [SerializeField]
    AudioMixerGroup sfxOutput;
    [SerializeField]
    AudioMixerGroup uiOutput;
    [SerializeField]
    AudioMixerGroup voiceOutput;
    [SerializeField]
    AudioMixerGroup ambienceOutput;
    [SerializeField]
    [Min(1)]
    int initialSourceCount = 8;
    [SerializeField]
    [Min(1)]
    int maxSourceCount = 24;
    [SerializeField]
    string masterVolumeParameter = "MasterVolume";
    [SerializeField]
    string bgmVolumeParameter = "BgmVolume";
    [SerializeField]
    string sfxVolumeParameter = "SfxVolume";
    [SerializeField]
    string uiVolumeParameter = "UIVolume";
    [SerializeField]
    string voiceVolumeParameter = "VoiceVolume";
    [SerializeField]
    string ambienceVolumeParameter = "AmbienceVolume";

    public AudioCatalog Catalog => catalog;
    public AudioMixer Mixer => mixer;
    public int InitialSourceCount => initialSourceCount;
    public int MaxSourceCount => Mathf.Max(initialSourceCount, maxSourceCount);

    /// <summary>
    /// 返回指定总线的输出组；未覆盖的枚举值回退到 Master。
    /// </summary>
    public AudioMixerGroup GetOutput(AudioBus bus)
    {
        return bus switch
        {
            AudioBus.Master => masterOutput,
            AudioBus.Bgm => bgmOutput,
            AudioBus.Sfx => sfxOutput,
            AudioBus.UI => uiOutput,
            AudioBus.Voice => voiceOutput,
            AudioBus.Ambience => ambienceOutput,
            _ => masterOutput
        };
    }

    /// <summary>
    /// 返回 AudioMixer 中对应的暴露参数名，用于运行时调节总线音量。
    /// </summary>
    public string GetVolumeParameter(AudioBus bus)
    {
        return bus switch
        {
            AudioBus.Master => masterVolumeParameter,
            AudioBus.Bgm => bgmVolumeParameter,
            AudioBus.Sfx => sfxVolumeParameter,
            AudioBus.UI => uiVolumeParameter,
            AudioBus.Voice => voiceVolumeParameter,
            AudioBus.Ambience => ambienceVolumeParameter,
            _ => masterVolumeParameter
        };
    }
}
