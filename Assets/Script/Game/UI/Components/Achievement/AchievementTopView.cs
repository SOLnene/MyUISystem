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
        // 顶部只订阅跨分类已领取总数，具体分类进度由左侧 Tab 自己展示。
        bindDisposables.Clear();
        countInfo
            .Subscribe(info =>
            {
                completionText.text =
                    $"已完成成就 {info.ClaimedCount} / {info.TotalCount}";
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
