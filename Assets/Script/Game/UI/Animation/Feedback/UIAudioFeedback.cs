using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 独立于 UIThreeStateSelectable 的交互音反馈，视觉状态刷新不会触发声音。
/// 组件未挂载到 Prefab 时不会影响现有 UI 行为。
/// </summary>
public sealed class UIAudioFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerClickHandler,
    ISelectHandler,
    ISubmitHandler
{
    [SerializeField]
    Selectable selectable;
    [SerializeField]
    UIAudioFeedbackPreset preset;

    void Reset()
    {
        selectable = GetComponent<Selectable>();
    }

    void Awake()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Play(preset != null ? preset.HoverCue : null);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Play(preset != null ? preset.ClickCue : null);
    }

    public void OnSelect(BaseEventData eventData)
    {
        Play(preset != null ? preset.SelectCue : null);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Play(preset != null ? preset.SubmitCue : null);
    }

    void Play(AudioCue cue)
    {
        if (cue == null || selectable != null && !selectable.IsInteractable())
        {
            return;
        }

        AudioManager.Instance.Play(cue);
    }
}
