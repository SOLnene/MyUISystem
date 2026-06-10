using UnityEngine;
using UnityEngine.EventSystems;

public class MainMenuButtonHoverFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private GameObject selectFrame;

    private bool pointerInside;
    private bool pointerPressed;

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerPressed = false;
        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerPressed = true;
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerPressed = false;
        Refresh();
    }

    private void OnDisable()
    {
        pointerInside = false;
        pointerPressed = false;
        Refresh();
    }

    private void Refresh()
    {
        selectFrame.SetActive(pointerInside || pointerPressed);
    }
}
