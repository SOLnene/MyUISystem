using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class RefineNormalPreviewFXView : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI valueText;
    [SerializeField]
    TextMeshProUGUI labelText;
    [SerializeField]
    float startOffsetY = -18f;
    [SerializeField]
    float valueFadeDuration = 0.08f;
    [SerializeField]
    float valueMoveDuration = 0.22f;
    [SerializeField]
    float valueSettleDuration = 0.08f;
    [SerializeField]
    float labelFadeDuration = 0.12f;
    [SerializeField]
    float startScale = 1.08f;
    [SerializeField]
    float peakScale = 1.22f;

    Sequence sequence;
    Vector2 valueDefaultPos;
    bool hasValueDefaultPos;

    public void ShowImmediate()
    {
        Prepare();
        valueText.alpha = 1f;
        labelText.alpha = 1f;
        valueText.rectTransform.anchoredPosition = valueDefaultPos;
        valueText.rectTransform.localScale = Vector3.one;
    }

    public async UniTask PlayReturn()
    {
        Prepare();

        RectTransform valueRoot = valueText.rectTransform;
        valueText.alpha = 0f;
        labelText.alpha = 0f;
        valueRoot.anchoredPosition = valueDefaultPos + new Vector2(0f, startOffsetY);
        valueRoot.localScale = Vector3.one * startScale;

        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(valueText.DOFade(1f, valueFadeDuration).SetEase(Ease.OutQuad));
        sequence.Join(valueRoot.DOAnchorPos(valueDefaultPos, valueMoveDuration).SetEase(Ease.OutCubic));
        sequence.Join(valueRoot.DOScale(peakScale, valueMoveDuration).SetEase(Ease.OutBack));
        sequence.Append(valueRoot.DOScale(1f, valueSettleDuration).SetEase(Ease.OutQuad));
        sequence.Append(labelText.DOFade(1f, labelFadeDuration).SetEase(Ease.OutQuad));

        await sequence.AsyncWaitForCompletion().AsUniTask();
        sequence = null;
    }

    void Prepare()
    {
        CacheDefaultPosition();
        KillSequence();
    }

    void CacheDefaultPosition()
    {
        if (hasValueDefaultPos)
            return;

        valueDefaultPos = valueText.rectTransform.anchoredPosition;
        hasValueDefaultPos = true;
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
