using System;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class StoreTopView : MonoBehaviour
{
    const string Title = "商城";

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

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => onClose?.Invoke());
    }

    public void BindCurrencies(IReadOnlyList<int> itemIds)
    {
        disposable.Clear();
        CurrencyValueView[] views = { starglitterView, stardustView, primogemView };
        for (int i = 0; i < views.Length; i++)
        {
            bool isVisible = i < itemIds.Count;
            views[i].gameObject.SetActive(isVisible);
            if (isVisible)
            {
                BindCurrency(views[i], itemIds[i]);
            }
        }
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
