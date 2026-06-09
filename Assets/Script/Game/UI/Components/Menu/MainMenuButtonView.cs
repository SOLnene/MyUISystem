using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuButtonView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI actionName;
    [SerializeField] private GameObject redDot;
    [SerializeField] private GameObject selectFrame;

    private MainMenuButtonData data;
    private Action<MainMenuAction> onClicked;
    private IDisposable redDotSubscription;
    private bool pointerInside;
    private bool pointerPressed;

    public void Bind(
        MainMenuButtonData buttonData,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> clickHandler)
    {
        data = buttonData;
        onClicked = clickHandler;

        button.transition = Selectable.Transition.None;
        button.interactable = data.interactable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);

        icon.sprite = data.icon;
        icon.enabled = data.icon != null;
        actionName.text = data.label;

        pointerInside = false;
        pointerPressed = false;
        RefreshSelection();

        redDotSubscription?.Dispose();
        redDot.SetActive(false);
        redDotSubscription = redDotProvider
            .Observe(data.redDotKey)
            .Subscribe(redDot.SetActive);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        RefreshSelection();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerPressed = false;
        RefreshSelection();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerPressed = true;
        RefreshSelection();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerPressed = false;
        RefreshSelection();
    }

    private void HandleClick()
    {
        onClicked?.Invoke(data.action);
    }

    private void RefreshSelection()
    {
        selectFrame.SetActive(button.interactable && (pointerInside || pointerPressed));
    }

    private void OnDisable()
    {
        pointerInside = false;
        pointerPressed = false;
        RefreshSelection();
    }

    private void OnDestroy()
    {
        redDotSubscription?.Dispose();
        redDotSubscription = null;
    }
}
