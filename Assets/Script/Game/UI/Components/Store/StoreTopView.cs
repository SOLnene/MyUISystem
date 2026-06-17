using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreTopView : MonoBehaviour
{
    const string Title = "商城";

    [SerializeField]
    TextMeshProUGUI titleText;
    [SerializeField]
    Button closeButton;

    public void Bind(Action onClose)
    {
        titleText.text = Title;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => onClose?.Invoke());
    }

    void OnDestroy()
    {
        closeButton.onClick.RemoveAllListeners();
    }
}
