using System.Collections.Generic;
using System;
using UnityEngine;

public class MenuActionPanel : MonoBehaviour
{
    private const string MainMenuButtonAddress = "ui/component/mainmenubutton";

    [SerializeField] private Transform buttonContent;

    private readonly List<MainMenuButtonView> activeButtons = new List<MainMenuButtonView>();
    private MainMenuButtonView buttonPrefab;
    private int bindVersion;

    public void Bind(
        IReadOnlyList<MainMenuButtonData> buttons,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> onButtonClicked)
    {
        Clear();
        if (buttons == null)
        {
            return;
        }

        var version = bindVersion;
        if (buttonPrefab != null)
        {
            CreateButtons(buttons, redDotProvider, onButtonClicked, version);
            return;
        }

        ResourceManager.Instance.LoadAssetAsync<GameObject>(MainMenuButtonAddress, prefab =>
        {
            if (version != bindVersion)
            {
                return;
            }

            if (prefab == null)
            {
                Debug.LogError($"MainMenuButton prefab not found: {MainMenuButtonAddress}");
                return;
            }

            buttonPrefab = prefab.GetComponent<MainMenuButtonView>();
            if (buttonPrefab == null)
            {
                Debug.LogError($"MainMenuButton prefab missing {nameof(MainMenuButtonView)}: {MainMenuButtonAddress}");
                return;
            }

            CreateButtons(buttons, redDotProvider, onButtonClicked, version);
        });
    }

    public void Clear()
    {
        bindVersion++;
        foreach (var button in activeButtons)
        {
            Destroy(button.gameObject);
        }

        activeButtons.Clear();
    }

    private void CreateButtons(
        IReadOnlyList<MainMenuButtonData> buttons,
        IMainMenuRedDotProvider redDotProvider,
        Action<MainMenuAction> onButtonClicked,
        int version)
    {
        if (version != bindVersion)
        {
            return;
        }

        foreach (var data in buttons)
        {
            var button = Instantiate(buttonPrefab, buttonContent);
            button.Bind(data, redDotProvider, onButtonClicked);
            activeButtons.Add(button);
        }
    }
}
