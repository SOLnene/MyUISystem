using DG.Tweening;
using UnityEngine;

public class MaterialAreaFeedbackView : MonoBehaviour
{
    [SerializeField]
    GameObject normalRoot;
    [SerializeField]
    CanvasGroup normalRootGroup;
    [SerializeField]
    CanvasGroup materialContentGroup;
    [SerializeField]
    RectTransform materialContentRoot;
    [SerializeField]
    MaterialResultFxView resultFxView;
    [SerializeField]
    GraphicStateVisual[] stateVisuals;
    [SerializeField]
    GameObject[] normalOnlyObjects;
    [SerializeField]
    float stateVisualDelayStep = 0.02f;
    [SerializeField]
    float fadeDuration = 0.12f;
    [SerializeField]
    float processingAlpha = 0.35f;
    [SerializeField]
    float materialEnterDuration = 0.18f;
    [SerializeField]
    float materialEnterOffsetY = 8f;

    Sequence sequence;
    Vector2 materialDefaultPos;
    bool hasMaterialDefaultPos;

    public void ShowProcessing()
    {
        Prepare();
        normalRoot.SetActive(true);
        resultFxView.Hide();
        SetNormalOnlyObjectsVisible(false);

        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(normalRootGroup.DOFade(processingAlpha, fadeDuration).SetEase(Ease.OutQuad));
        sequence.Join(materialContentGroup.DOFade(0f, fadeDuration).SetEase(Ease.OutQuad));
        AppendStateVisuals(sequence, GraphicStateVisual.State.Processing);
        sequence.OnComplete(resultFxView.ShowLoading);
    }

    public void ShowNormal(bool playMaterialEnter)
    {
        Prepare();
        normalRoot.SetActive(true);
        resultFxView.Hide();
        SetNormalOnlyObjectsVisible(true);

        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(normalRootGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad));
        AppendStateVisuals(sequence, GraphicStateVisual.State.Normal);

        if (playMaterialEnter)
            AppendMaterialEnter(sequence);
        else
            materialContentGroup.alpha = 1f;
    }

    public void ShowResultText(string text)
    {
        Prepare();
        SetNormalOnlyObjectsVisible(false);
        normalRootGroup.alpha = 0f;
        materialContentGroup.alpha = 0f;
        normalRoot.SetActive(false);
        resultFxView.ShowMaxText(text);
    }

    public void HideResult()
    {
        Prepare();
        resultFxView.Hide();
    }

    void Prepare()
    {
        CacheMaterialDefaultPosition();
        KillSequence();
    }

    void AppendMaterialEnter(Sequence targetSequence)
    {
        materialContentGroup.alpha = 0f;
        materialContentRoot.anchoredPosition = materialDefaultPos + new Vector2(0f, materialEnterOffsetY);

        targetSequence.Join(materialContentGroup.DOFade(1f, materialEnterDuration).SetEase(Ease.OutQuad));
        targetSequence.Join(materialContentRoot.DOAnchorPos(materialDefaultPos, materialEnterDuration).SetEase(Ease.OutCubic));
    }

    void AppendStateVisuals(Sequence targetSequence, GraphicStateVisual.State state)
    {
        for (int i = 0; i < stateVisuals.Length; i++)
            stateVisuals[i].AppendTo(targetSequence, state, i * stateVisualDelayStep);
    }

    void SetNormalOnlyObjectsVisible(bool visible)
    {
        for (int i = 0; i < normalOnlyObjects.Length; i++)
            normalOnlyObjects[i].SetActive(visible);
    }

    void CacheMaterialDefaultPosition()
    {
        if (hasMaterialDefaultPos)
            return;

        materialDefaultPos = materialContentRoot.anchoredPosition;
        hasMaterialDefaultPos = true;
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
