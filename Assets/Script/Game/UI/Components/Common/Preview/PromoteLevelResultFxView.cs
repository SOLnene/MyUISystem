using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public readonly struct PromoteLevelResultData
{
    public readonly int oldRank;
    public readonly int newRank;
    public readonly int currentLevel;
    public readonly int oldMaxLevel;
    public readonly int newMaxLevel;
    public readonly Color rarityColor;

    public PromoteLevelResultData(int oldRank, int newRank, int currentLevel, int oldMaxLevel, int newMaxLevel, Color rarityColor)
    {
        this.oldRank = oldRank;
        this.newRank = newRank;
        this.currentLevel = currentLevel;
        this.oldMaxLevel = oldMaxLevel;
        this.newMaxLevel = newMaxLevel;
        this.rarityColor = rarityColor;
    }
}

public class PromoteLevelResultFxView : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("resultFxContent")]
    GameObject resultFxRoot;
    [SerializeField]
    CanvasGroup beforeGroup;
    [SerializeField]
    CanvasGroup afterGroup;
    [SerializeField]
    RectTransform afterRoot;
    [SerializeField]
    LevelCapLineView beforeLevelLine;
    [SerializeField]
    LevelCapLineView afterLevelLine;
    [SerializeField]
    Image bgImage;
    [SerializeField]
    Graphic[] beforeStars;
    [SerializeField]
    Graphic[] afterStars;
    [SerializeField]
    Color activeStarColor = Color.white;
    [SerializeField]
    Color inactiveStarColor = new Color(1f, 1f, 1f, 0.45f);

    Tween activeTween;

    const float HoldBeforeDuration = 0.14f;
    const float BeforeFadeDuration = 0.1f;
    const float AfterEnterDuration = 0.22f;
    const float HoldAfterDuration = 0.5f;
    const float ExitDuration = 0.16f;
    const float AfterStartScale = 1.18f;
    const float NewStarStartScale = 0.6f;
    const float NewStarPopScale = 1.25f;
    const float NewStarPopDuration = 0.18f;

    void Awake()
    {
        HideResultImmediate();
    }

    public async UniTask Play(PromoteLevelResultData data, Action onNewStateShown = null)
    {
        KillActiveTween();
        Setup(data);
        await UniTask.Delay(TimeSpan.FromSeconds(HoldBeforeDuration));
        await SwitchToAfter(data, onNewStateShown);
        await UniTask.Delay(TimeSpan.FromSeconds(HoldAfterDuration));
        await PlayExit();
        HideResultImmediate();
    }

    public void HideResultImmediate()
    {
        KillActiveTween();

        resultFxRoot.SetActive(false);
        beforeGroup.alpha = 0f;
        afterGroup.alpha = 0f;
        afterRoot.localScale = Vector3.one;
    }

    void Setup(PromoteLevelResultData data)
    {
        resultFxRoot.SetActive(true);

        beforeLevelLine.SetValueAndState(data.currentLevel, data.oldMaxLevel, LevelCapLineView.VisualState.ValuesNormal);
        afterLevelLine.SetValueAndState(data.currentLevel, data.newMaxLevel, LevelCapLineView.VisualState.MaxHighlighted);
        bgImage.color = data.rarityColor;
        SetStars(beforeStars, data.oldRank);
        SetStars(afterStars, data.newRank);

        beforeGroup.alpha = 1f;
        afterGroup.alpha = 0f;
        afterRoot.localScale = Vector3.one * AfterStartScale;
        ResetStarScales(afterStars);
    }

    async UniTask SwitchToAfter(PromoteLevelResultData data, Action onNewStateShown)
    {
        activeTween = beforeGroup.DOFade(0f, BeforeFadeDuration).SetEase(Ease.OutQuad);
        await activeTween.AsyncWaitForCompletion().AsUniTask();
        activeTween = null;

        onNewStateShown?.Invoke();
        PlayNewStarPop(data);

        await UniTask.WhenAll(
            afterGroup.DOFade(1f, AfterEnterDuration * 0.75f)
                .SetEase(Ease.OutQuad)
                .AsyncWaitForCompletion()
                .AsUniTask(),
            afterRoot.DOScale(1f, AfterEnterDuration)
                .SetEase(Ease.OutBack)
                .AsyncWaitForCompletion()
                .AsUniTask()
        );
    }

    async UniTask PlayExit()
    {
        activeTween = afterGroup.DOFade(0f, ExitDuration).SetEase(Ease.OutQuad);
        await activeTween.AsyncWaitForCompletion().AsUniTask();
        activeTween = null;
    }

    void SetStars(Graphic[] stars, int rank)
    {
        for (int i = 0; i < stars.Length; i++)
            stars[i].color = i < rank ? activeStarColor : inactiveStarColor;
    }

    void ResetStarScales(Graphic[] stars)
    {
        for (int i = 0; i < stars.Length; i++)
            stars[i].rectTransform.localScale = Vector3.one;
    }

    void PlayNewStarPop(PromoteLevelResultData data)
    {
        int firstNewStar = Mathf.Clamp(data.oldRank, 0, afterStars.Length);
        int lastNewStar = Mathf.Clamp(data.newRank, 0, afterStars.Length);

        for (int i = firstNewStar; i < lastNewStar; i++)
        {
            RectTransform star = afterStars[i].rectTransform;
            star.localScale = Vector3.one * NewStarStartScale;
            star.DOScale(NewStarPopScale, NewStarPopDuration * 0.55f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    star.DOScale(1f, NewStarPopDuration * 0.45f).SetEase(Ease.OutQuad);
                });
        }
    }

    void OnDestroy()
    {
        KillActiveTween();
    }

    void KillActiveTween()
    {
        activeTween?.Kill();
        activeTween = null;
    }
}
