using UnityEngine;

/// <summary>
/// 可复用的 UI 交互音预设，避免每个按钮重复配置相同的 Cue 组合。
/// </summary>
[CreateAssetMenu(fileName = "UIAudioFeedbackPreset", menuName = "Game/Audio/UI Feedback Preset")]
public sealed class UIAudioFeedbackPreset : ScriptableObject
{
    [SerializeField]
    AudioCue hoverCue;
    [SerializeField]
    AudioCue selectCue;
    [SerializeField]
    AudioCue clickCue;
    [SerializeField]
    AudioCue submitCue;

    public AudioCue HoverCue => hoverCue;
    public AudioCue SelectCue => selectCue;
    public AudioCue ClickCue => clickCue;
    public AudioCue SubmitCue => submitCue;
}
