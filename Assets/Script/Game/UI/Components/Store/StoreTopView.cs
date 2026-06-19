using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class StoreTopView : MonoBehaviour
{
    const string Title = "商城";
    const int StarglitterItemId = 223;
    const int StardustItemId = 224;
    const int PrimogemItemId = 203;

    [SerializeField]
    TextMeshProUGUI titleText;
    [SerializeField]
    Button closeButton;
    [SerializeField]
    CurrencyValueView starglitterView;
    [SerializeField]
    CurrencyValueView stardustView;
    [SerializeField]
    CurrencyValueView primogemView;

    readonly CompositeDisposable disposable = new();

    public void Bind(Action onClose)
    {
        disposable.Clear();
        titleText.text = Title;
        BindCurrency(starglitterView, StarglitterItemId);
        BindCurrency(stardustView, StardustItemId);
        BindCurrency(primogemView, PrimogemItemId);

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => onClose?.Invoke());
    }

    void BindCurrency(CurrencyValueView view, int itemId)
    {
        ItemDefinition itemDefinition = GameDatabase.ItemDatabase.GetItemByID(itemId);
        view.Bind(itemDefinition.iconPath, GameEconomy.Instance.GetCurrency(itemId));
        GameEconomy.Instance.ObserveCurrency(itemId)
            .Subscribe(view.SetAmount)
            .AddTo(disposable);
    }

    void OnDestroy()
    {
        disposable.Dispose();
        closeButton.onClick.RemoveAllListeners();
    }
}
