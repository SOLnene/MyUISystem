using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class RefineRankResultFxView : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI oldRankText;
    [SerializeField]
    TextMeshProUGUI newRankText;
    [SerializeField]
    ResultAccentFxView resultAccentFxView;
    [SerializeField]
    ParticleSystem[] resultParticles;
    [SerializeField]
    float holdOldDuration = 0.12f;
    [SerializeField]
    float switchDuration = 0.22f;
    [SerializeField]
    float resultAccentDuration = 0.18f;
    [SerializeField]
    float exitDuration = 0.2f;
    [SerializeField]
    float oldExitScale = 1.16f;
    [SerializeField]
    float newStartScale = 1.16f;
    [SerializeField]
    float oldMoveOffsetY = 18f;
    [SerializeField]
    float newStartOffsetY = -20f;

    Vector2 oldDefaultPos;
    Vector2 newDefaultPos;
    bool hasDefaultPos;
    Sequence sequence;

    void Awake()
    {
        CacheDefaultPosition();
        HideImmediate();
    }

    public async UniTask Play(int oldRank, int newRank, Action onResultAccentComplete = null)
    {
        CacheDefaultPosition();
        KillSequence();
        Setup(oldRank, newRank);

        await UniTask.Delay(TimeSpan.FromSeconds(holdOldDuration));
        await PlaySwitch();
        await PlayResultAccent();
        onResultAccentComplete?.Invoke();
        await PlayExit();
    }

    public void HideImmediate()
    {
        KillSequence();
        gameObject.SetActive(false);
        oldRankText.alpha = 0f;
        newRankText.alpha = 0f;
        oldRankText.rectTransform.anchoredPosition = oldDefaultPos;
        newRankText.rectTransform.anchoredPosition = newDefaultPos;
        oldRankText.rectTransform.localScale = Vector3.one;
        newRankText.rectTransform.localScale = Vector3.one;
    }

    void Setup(int oldRank, int newRank)
    {
        gameObject.SetActive(true);

        oldRankText.text = $"{oldRank}阶";
        newRankText.text = $"{newRank}阶";

        oldRankText.alpha = 1f;
        newRankText.alpha = 0f;
        oldRankText.rectTransform.anchoredPosition = oldDefaultPos;
        newRankText.rectTransform.anchoredPosition = newDefaultPos + new Vector2(0f, newStartOffsetY);
        oldRankText.rectTransform.localScale = Vector3.one;
        newRankText.rectTransform.localScale = Vector3.one * newStartScale;
    }

    async UniTask PlaySwitch()
    {
        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(oldRankText.DOFade(0f, switchDuration).SetEase(Ease.OutQuad));
        sequence.Join(oldRankText.rectTransform
            .DOAnchorPos(oldDefaultPos + new Vector2(0f, oldMoveOffsetY), switchDuration)
            .SetEase(Ease.OutCubic));
        sequence.Join(oldRankText.rectTransform
            .DOScale(oldExitScale, switchDuration)
            .SetEase(Ease.OutCubic));
        sequence.Join(newRankText.DOFade(1f, switchDuration).SetEase(Ease.OutQuad));

        await sequence.AsyncWaitForCompletion().AsUniTask();
        sequence = null;
    }

    async UniTask PlayResultAccent()
    {
        resultAccentFxView?.Play();

        if (resultParticles != null)
        {
            for (int i = 0; i < resultParticles.Length; i++)
                if (resultParticles[i] != null)
                    resultParticles[i].Play(true);
        }

        if (resultAccentDuration > 0f)
            await UniTask.Delay(TimeSpan.FromSeconds(resultAccentDuration));
    }

    async UniTask PlayExit()
    {
        RectTransform newRoot = newRankText.rectTransform;

        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(newRoot.DOAnchorPos(newDefaultPos, exitDuration).SetEase(Ease.InOutCubic));
        sequence.Join(newRoot.DOScale(Vector3.one, exitDuration).SetEase(Ease.OutCubic));

        await sequence.AsyncWaitForCompletion().AsUniTask();
        sequence = null;
    }

    void CacheDefaultPosition()
    {
        if (hasDefaultPos)
            return;

        oldDefaultPos = oldRankText.rectTransform.anchoredPosition;
        newDefaultPos = newRankText.rectTransform.anchoredPosition;
        hasDefaultPos = true;
    }

    void KillSequence()
    {
        if (sequence == null)
            return;

        sequence.Kill();
        sequence = null;
    }

    void OnDestroy()
    {
        KillSequence();
    }
}
