using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI actionName;
    [SerializeField] private GameObject redDot;

    private MainMenuButtonData data;
    private Action<MainMenuAction> onClicked;
    private readonly CompositeDisposable bindDisposables = new CompositeDisposable();

    public void Bind(
        MainMenuButtonData buttonData,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> clickHandler)
    {
        bindDisposables.Clear();
        data = buttonData;
        onClicked = clickHandler;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);

        icon.sprite = data.icon;
        icon.enabled = data.icon != null;
        actionName.text = data.label;
        redDotProvider
            .Observe(data.redDotKey)
            .Subscribe(redDot.SetActive)
            .AddTo(bindDisposables);
    }

    private void HandleClick()
    {
        onClicked?.Invoke(data.action);
    }

    private void OnDestroy()
    {
        bindDisposables.Dispose();
        button.onClick.RemoveListener(HandleClick);
    }
}
