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
    private bool isCharacterTutorialTarget;

    public void Bind(
        MainMenuButtonData buttonData,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> clickHandler)
    {
        UnregisterTutorialTarget();
        bindDisposables.Clear();
        data = buttonData;
        onClicked = clickHandler;
        isCharacterTutorialTarget = data.action == MainMenuAction.Character;
        RegisterTutorialTarget();

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

    private void OnEnable()
    {
        RegisterTutorialTarget();
    }

    private void OnDisable()
    {
        UnregisterTutorialTarget();
    }

    private void RegisterTutorialTarget()
    {
        if (isCharacterTutorialTarget)
        {
            TutorialTargetRegistry.Register(
                TutorialTargetId.MainMenuCharacter,
                transform as RectTransform,
                this);
        }
    }

    private void UnregisterTutorialTarget()
    {
        TutorialTargetRegistry.Unregister(TutorialTargetId.MainMenuCharacter, this);
    }

    private void OnDestroy()
    {
        UnregisterTutorialTarget();
        bindDisposables.Dispose();
        button.onClick.RemoveListener(HandleClick);
    }
}
