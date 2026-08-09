using UnityEngine;

/// <summary>
/// 音频总线分类，用于路由到不同的 AudioMixerGroup 并独立控制音量。
/// </summary>
public enum AudioBus
{
    Master,
    Bgm,
    Sfx,
    UI,
    Voice,
    Ambience
}

/// <summary>
/// 一次播放请求所需的静态配置，业务层只依赖 Cue，不直接持有 AudioSource。
/// </summary>
[CreateAssetMenu(fileName = "AudioCue", menuName = "Game/Audio/Audio Cue")]
public sealed class AudioCue : ScriptableObject
{
    [SerializeField]
    string id;
    [SerializeField]
    string clipAddress;
    [SerializeField]
    AudioBus bus = AudioBus.Sfx;
    [SerializeField]
    bool loop;
    [SerializeField]
    [Range(0f, 1f)]
    float volume = 1f;
    [SerializeField]
    Vector2 pitchRange = Vector2.one;
    [SerializeField]
    [Range(0f, 1f)]
    float spatialBlend;
    [SerializeField]
    [Min(1)]
    int maxInstances = 4;
    [SerializeField]
    [Min(0f)]
    float retriggerCooldown = 0.03f;
    [SerializeField]
    [Range(0, 256)]
    int priority = 128;

    public string Id => id;
    public string ClipAddress => clipAddress;
    public AudioBus Bus => bus;
    public bool Loop => loop;
    public float Volume => volume;
    public float MinPitch => Mathf.Min(pitchRange.x, pitchRange.y);
    public float MaxPitch => Mathf.Max(pitchRange.x, pitchRange.y);
    public float SpatialBlend => spatialBlend;
    public int MaxInstances => maxInstances;
    public float RetriggerCooldown => retriggerCooldown;
    public int Priority => priority;
}
