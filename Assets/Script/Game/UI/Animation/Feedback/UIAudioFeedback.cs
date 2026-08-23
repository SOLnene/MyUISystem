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
        PlayClick();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClick();
    }

    void PlayClick()
    {
        if (selectable != null && !selectable.IsInteractable())
        {
            return;
        }

        AudioManager.Instance.PlayUI(UISound.ButtonClick);
    }
}
