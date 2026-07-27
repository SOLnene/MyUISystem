using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class AchievementTopView : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI completionText;
    [SerializeField]
    Button closeButton;

    readonly CompositeDisposable bindDisposables = new();

    public void Bind(
        IReadOnlyReactiveProperty<AchievementCountInfo> countInfo,
        Action onClose)
    {
        bindDisposables.Clear();
        countInfo
            .Subscribe(info =>
            {
                completionText.text =
                    $"已完成成就 {info.CompletedCount} / {info.TotalCount}";
            })
            .AddTo(bindDisposables);
        closeButton
            .OnClickAsObservable()
            .Subscribe(_ => onClose?.Invoke())
            .AddTo(bindDisposables);
    }

    void OnDestroy()
    {
        bindDisposables.Dispose();
    }
}
