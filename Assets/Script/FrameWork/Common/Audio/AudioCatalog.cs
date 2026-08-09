using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AudioCue 配置库，支持通过稳定的语义 ID 发起播放请求。
/// </summary>
[CreateAssetMenu(fileName = "AudioCatalog", menuName = "Game/Audio/Audio Catalog")]
public sealed class AudioCatalog : ScriptableObject
{
    [SerializeField]
    List<AudioCue> cues = new();

    readonly Dictionary<string, AudioCue> lookup =
        new(StringComparer.Ordinal);
    bool lookupReady;

    public IReadOnlyList<AudioCue> Cues => cues;

    /// <summary>
    /// 查找 Cue。字典延迟构建，避免仅查看配置资产时产生额外初始化工作。
    /// </summary>
    public bool TryGet(string id, out AudioCue cue)
    {
        cue = null;
        if (!lookupReady)
        {
            RebuildLookup();
        }

        return !string.IsNullOrWhiteSpace(id) && lookup.TryGetValue(id, out cue);
    }

    void OnEnable()
    {
        RebuildLookup();
    }

    void OnValidate()
    {
        RebuildLookup();
    }

    void RebuildLookup()
    {
        lookup.Clear();
        for (int i = 0; i < cues.Count; i++)
        {
            AudioCue cue = cues[i];
            if (cue == null || string.IsNullOrWhiteSpace(cue.Id))
            {
                continue;
            }

            // 重复 ID 会造成播放目标不确定，因此保留第一项并显式告警。
            if (!lookup.TryAdd(cue.Id, cue))
            {
                Debug.LogWarning($"Audio cue id is duplicated: {cue.Id}", this);
            }
        }

        lookupReady = true;
    }
}
