using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 独立于 UIThreeStateSelectable 的交互音反馈，视觉状态刷新不会触发声音。
/// 组件未挂载到 Prefab 时不会影响现有 UI 行为。
/// </summary>
public sealed class UIAudioFeedback : MonoBehaviour,
    IPointerClickHandler,
    ISubmitHandler
{
    [SerializeField]
    Selectable selectable;
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] float volume = 1f;
    [SerializeField] bool muteClick;

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            PlayClick();
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClick();
    }

    void PlayClick()
    {
        if (muteClick || !isActiveAndEnabled ||
            (selectable != null && (!selectable.IsActive() || !selectable.IsInteractable())))
        {
            return;
        }

        if (clickClip != null)
            AudioManager.Instance.PlayUI(clickClip, volume);
        else
            AudioManager.Instance.PlayUI(UISound.ButtonClick);
    }
}
