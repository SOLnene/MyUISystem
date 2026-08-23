using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum UISound
{
    PanelOpen,
    PanelClose,
    ButtonClick
}

/// <summary>
/// 统一负责 AudioClip 异步加载、AudioSource 复用、并发限制和总线控制。
/// </summary>
public sealed class AudioManager : SingletonMono<AudioManager>
{
    sealed class ActivePlayback
    {
        public AudioCue Cue;
        public AudioSource Source;
    }

    [SerializeField]
    AudioSystemConfig config;
    [SerializeField]
    AudioClip panelOpenClip;
    [SerializeField]
    AudioClip panelCloseClip;
    [SerializeField]
    AudioClip buttonClickClip;
    [SerializeField]
    [Range(0f, 1f)]
    float panelOpenVolume = 0.75f;
    [SerializeField]
    [Range(0f, 1f)]
    float panelCloseVolume = 0.95f;
    [SerializeField]
    [Range(0f, 1f)]
    float buttonClickVolume = 0.8f;

    readonly List<AudioSource> sources = new();
    readonly Dictionary<int, ActivePlayback> activePlaybacks = new();
    readonly Dictionary<AudioCue, float> lastPlayedTimes = new();
    readonly List<int> completedHandles = new();

    AudioSource uiSource;
    Transform sourceRoot;
    int nextHandleId = 1;
    bool initialized;

    public bool IsInitialized => initialized;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        EnsureUISource();
        if (config != null)
        {
            Initialize(config);
        }
    }

    /// <summary>
    /// 播放常驻的基础 UI 音效，不经过异步资源加载。
    /// </summary>
    public void PlayUI(UISound sound)
    {
        if (uiSource == null)
        {
            EnsureUISource();
        }

        (AudioClip clip, float volume) = sound switch
        {
            UISound.PanelOpen => (panelOpenClip, panelOpenVolume),
            UISound.PanelClose => (panelCloseClip, panelCloseVolume),
            UISound.ButtonClick => (buttonClickClip, buttonClickVolume),
            _ => throw new ArgumentOutOfRangeException(nameof(sound), sound, null)
        };

        uiSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// 装配或替换音频系统配置。重复传入同一配置不会重建 Source 池。
    /// </summary>
    public bool Initialize(AudioSystemConfig systemConfig)
    {
        if (systemConfig == null)
        {
            Debug.LogError("Audio system config is missing.", this);
            return false;
        }

        if (initialized && config == systemConfig)
        {
            return true;
        }

        if (initialized)
        {
            StopAll();
        }

        config = systemConfig;
        initialized = true;
        EnsureSourceRoot();
        EnsurePoolSize(config.InitialSourceCount);
        return true;
    }

    public void Play(AudioCue cue)
    {
        PlayAsync(cue).Forget();
    }

    public void Play(string cueId)
    {
        PlayAsync(cueId).Forget();
    }

    /// <summary>
    /// 通过 Catalog 中的语义 ID 播放音频。
    /// </summary>
    public async UniTask<AudioPlaybackHandle> PlayAsync(
        string cueId,
        Vector3? position = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCue(cueId, out AudioCue cue))
        {
            return default;
        }

        return await PlayAsync(cue, position, cancellationToken);
    }

    /// <summary>
    /// 播放指定 Cue。等待取消只取消当前调用方，不改变 ResourceManager 的共享加载策略。
    /// </summary>
    public async UniTask<AudioPlaybackHandle> PlayAsync(
        AudioCue cue,
        Vector3? position = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanRequestPlayback(cue))
        {
            return default;
        }

        // 在异步加载前占用冷却窗口，避免同一帧的并发请求一起穿透限制。
        lastPlayedTimes[cue] = Time.unscaledTime;
        AudioClip clip = await ResourceManager.Instance.LoadAssetAsync<AudioClip>(
            cue.ClipAddress,
            cancellationToken);
        if (clip == null || cancellationToken.IsCancellationRequested)
        {
            return default;
        }

        // 加载期间可能已有同类播放开始，播放前必须再次检查并发上限。
        CleanupFinishedPlaybacks();
        if (CountActiveInstances(cue) >= cue.MaxInstances)
        {
            return default;
        }

        AudioSource source = AcquireSource();
        if (source == null)
        {
            return default;
        }

        source.clip = clip;
        source.outputAudioMixerGroup = config.GetOutput(cue.Bus);
        source.loop = cue.Loop;
        source.volume = cue.Volume;
        source.pitch = UnityEngine.Random.Range(cue.MinPitch, cue.MaxPitch);
        source.spatialBlend = cue.SpatialBlend;
        source.priority = cue.Priority;
        source.transform.position = position ?? Vector3.zero;

        int handleId = GetNextHandleId();
        activePlaybacks.Add(handleId, new ActivePlayback
        {
            Cue = cue,
            Source = source
        });
        source.Play();
        return new AudioPlaybackHandle(handleId);
    }

    /// <summary>
    /// 停止句柄对应的播放；播放已自然结束时返回 false。
    /// </summary>
    public bool Stop(AudioPlaybackHandle handle)
    {
        if (!handle.IsValid || !activePlaybacks.TryGetValue(handle.Id, out ActivePlayback playback))
        {
            return false;
        }

        ReleasePlayback(handle.Id, playback);
        return true;
    }

    public void Stop(AudioBus bus)
    {
        completedHandles.Clear();
        foreach (KeyValuePair<int, ActivePlayback> pair in activePlaybacks)
        {
            if (pair.Value.Cue.Bus == bus)
            {
                completedHandles.Add(pair.Key);
            }
        }

        ReleaseCompletedHandles();
    }

    public void StopAll()
    {
        completedHandles.Clear();
        completedHandles.AddRange(activePlaybacks.Keys);
        ReleaseCompletedHandles();
    }

    /// <summary>
    /// 使用 0～1 线性值设置总线音量，并转换为 AudioMixer 使用的分贝值。
    /// </summary>
    public bool SetBusVolume(AudioBus bus, float linearVolume)
    {
        if (!initialized || config.Mixer == null)
        {
            return false;
        }

        string parameter = config.GetVolumeParameter(bus);
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return false;
        }

        float clampedVolume = Mathf.Clamp01(linearVolume);
        float decibels = clampedVolume <= 0.0001f
            ? -80f
            : Mathf.Log10(clampedVolume) * 20f;
        return config.Mixer.SetFloat(parameter, decibels);
    }

    void Update()
    {
        CleanupFinishedPlaybacks();
    }

    bool TryGetCue(string cueId, out AudioCue cue)
    {
        cue = null;
        if (!initialized || config.Catalog == null)
        {
            Debug.LogWarning("AudioManager is not initialized with a catalog.", this);
            return false;
        }

        if (config.Catalog.TryGet(cueId, out cue))
        {
            return true;
        }

        Debug.LogWarning($"Audio cue was not found: {cueId}", this);
        return false;
    }

    bool CanRequestPlayback(AudioCue cue)
    {
        if (!initialized)
        {
            Debug.LogWarning("AudioManager is not initialized.", this);
            return false;
        }

        if (cue == null || string.IsNullOrWhiteSpace(cue.ClipAddress))
        {
            return false;
        }

        CleanupFinishedPlaybacks();
        if (CountActiveInstances(cue) >= cue.MaxInstances)
        {
            return false;
        }

        return !lastPlayedTimes.TryGetValue(cue, out float lastPlayedAt) ||
               Time.unscaledTime - lastPlayedAt >= cue.RetriggerCooldown;
    }

    int CountActiveInstances(AudioCue cue)
    {
        int count = 0;
        foreach (ActivePlayback playback in activePlaybacks.Values)
        {
            if (playback.Cue == cue)
            {
                count++;
            }
        }

        return count;
    }

    AudioSource AcquireSource()
    {
        // 先复用空闲 Source，仅在池未达到上限时扩容。
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i].clip == null)
            {
                return sources[i];
            }
        }

        if (sources.Count >= config.MaxSourceCount)
        {
            return null;
        }

        return CreateSource();
    }

    void EnsureSourceRoot()
    {
        if (sourceRoot != null)
        {
            return;
        }

        var root = new GameObject("AudioSources");
        root.transform.SetParent(transform, false);
        sourceRoot = root.transform;
    }

    void EnsureUISource()
    {
        // 基础 UI 音效常驻并共享一个 PlayOneShot Source，避免首次交互等待异步加载。
        uiSource = GetComponent<AudioSource>();
        if (uiSource == null)
        {
            uiSource = gameObject.AddComponent<AudioSource>();
        }

        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
    }

    void EnsurePoolSize(int targetSize)
    {
        while (sources.Count < targetSize)
        {
            CreateSource();
        }
    }

    AudioSource CreateSource()
    {
        var sourceObject = new GameObject($"AudioSource_{sources.Count}");
        sourceObject.transform.SetParent(sourceRoot, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        sources.Add(source);
        return source;
    }

    void CleanupFinishedPlaybacks()
    {
        // 遍历期间不修改字典，先收集句柄再统一释放。
        completedHandles.Clear();
        foreach (KeyValuePair<int, ActivePlayback> pair in activePlaybacks)
        {
            if (!pair.Value.Source.isPlaying)
            {
                completedHandles.Add(pair.Key);
            }
        }

        ReleaseCompletedHandles();
    }

    void ReleaseCompletedHandles()
    {
        for (int i = 0; i < completedHandles.Count; i++)
        {
            int handleId = completedHandles[i];
            if (activePlaybacks.TryGetValue(handleId, out ActivePlayback playback))
            {
                ReleasePlayback(handleId, playback);
            }
        }

        completedHandles.Clear();
    }

    void ReleasePlayback(int handleId, ActivePlayback playback)
    {
        // 清空所有与上一次播放相关的状态，避免复用时泄漏路由或循环设置。
        playback.Source.Stop();
        playback.Source.clip = null;
        playback.Source.loop = false;
        playback.Source.outputAudioMixerGroup = null;
        playback.Source.transform.localPosition = Vector3.zero;
        activePlaybacks.Remove(handleId);
    }

    int GetNextHandleId()
    {
        while (nextHandleId == 0 || activePlaybacks.ContainsKey(nextHandleId))
        {
            nextHandleId++;
        }

        return nextHandleId++;
    }

    void OnDestroy()
    {
        StopAll();
    }
}
