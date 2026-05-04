using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class UITopBar : MonoBehaviour
{
    [SerializeField]
    Button backBtn;
    [SerializeField]
    TextMeshProUGUI titleText;
    [SerializeField]
    TextMeshProUGUI goldText;

    CompositeDisposable disposable = new CompositeDisposable();
    public void Bind(string label, ReactiveProperty<int> goldRP, Action onBack)
    {
        disposable.Clear();
        titleText.text = label;
        goldRP.Subscribe(gold =>
        {
            goldText.text = $"{gold}";
        }).AddTo(disposable);
        backBtn.onClick.RemoveAllListeners();
        backBtn.onClick.AddListener(() => onBack?.Invoke());
    }
    public void OnDestroy()
    {
        disposable.Dispose();
    }
}
