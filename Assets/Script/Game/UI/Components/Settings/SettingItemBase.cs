using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingItemBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image hoverOutline;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hoverOutline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverOutline.enabled = false;
    }

    void OnDisable()
    {
        // Cached rows must not reopen with a stale hover state.
        hoverOutline.enabled = false;
    }
}
