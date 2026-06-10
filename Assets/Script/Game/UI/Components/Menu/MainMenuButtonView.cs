using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI actionName;

    private MainMenuButtonData data;
    private Action<MainMenuAction> onClicked;

    public void Bind(
        MainMenuButtonData buttonData,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> clickHandler)
    {
        data = buttonData;
        onClicked = clickHandler;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);

        icon.sprite = data.icon;
        icon.enabled = data.icon != null;
        actionName.text = data.label;
    }

    private void HandleClick()
    {
        onClicked?.Invoke(data.action);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveListener(HandleClick);
    }
}
