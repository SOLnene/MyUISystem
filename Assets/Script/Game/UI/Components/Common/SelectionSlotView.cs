using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SelectionSlotView : UIThreeStateSelectable
{
    [SerializeField]
    Image icon;
    [SerializeField]
    Image rarityBackground;
    [SerializeField]
    Image glowEffectImage;
    [SerializeField]
    Transform scaleRoot;
    [SerializeField]
    Button button;

    Tween hoverTween;
    Tween loopTween;
    CancellationTokenSource loadCts;

    Transform ScaleTarget => scaleRoot != null ? scaleRoot : transform;

    public Button Button => button;

    public void SetInteractable(bool interactable)
    {
        button.interactable = interactable;
    }

    public void SetClickListener(UnityAction onClick)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
    }

    public void LoadIcon(string iconPath)
    {
        CancelIconLoad();
        loadCts = new CancellationTokenSource();
        IconLoader.SetSpriteAsync(icon, iconPath, loadCts.Token).Forget();
    }

    public void BindVisual(string iconPath, Color rarityColor)
    {
        SetRarityColor(rarityColor);
        LoadIcon(iconPath);
    }

    public void SetRarityColor(Color rarityColor)
    {
        rarityBackground.color = rarityColor;
    }

    public void ClearIcon()
    {
        icon.sprite = null;
    }

    public void ResetState()
    {
        CancelIconLoad();
        button.onClick.RemoveAllListeners();
        hoverTween?.Kill();
        loopTween?.Kill();
        hoverTween = null;
        loopTween = null;
        rarityBackground.color = Color.white;
        SetSelected(false, true);
        ClearIcon();
    }

    void CancelIconLoad()
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = null;
    }

    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        hoverTween?.Kill();
        loopTween?.Kill();

        switch (state)
        {
            case VisualState.Normal:
                ApplyNormalState(instant || !stateChanged);
                break;
            case VisualState.Hover:
                ApplyHoverState(instant || !stateChanged);
                break;
            case VisualState.Selected:
                ApplySelectedState(instant || !stateChanged);
                break;
        }
    }

    void ApplyNormalState(bool instant)
    {
        SetGlowScale(Vector3.one);
        if (instant)
        {
            ScaleTarget.localScale = Vector3.one;
            SetGlowAlpha(0f);
            return;
        }

        hoverTween = DOTween.Sequence()
            .Append(ScaleTarget.DOScale(1.0f, 0.1f).SetEase(Ease.OutQuad))
            .Join(glowEffectImage.transform.DOScale(1.0f, 0.1f).SetEase(Ease.OutQuad))
            .Join(glowEffectImage.DOFade(0.0f, 0.1f))
            .SetUpdate(true);
    }

    void ApplyHoverState(bool instant)
    {
        SetGlowScale(Vector3.one);
        if (instant)
        {
            ScaleTarget.localScale = Vector3.one * 1.035f;
            SetGlowAlpha(0.55f);
            return;
        }

        hoverTween = DOTween.Sequence()
            .Append(ScaleTarget.DOScale(1.035f, 0.08f).SetEase(Ease.OutQuad))
            .Join(glowEffectImage.transform.DOScale(1.02f, 0.08f).SetEase(Ease.OutQuad))
            .Join(glowEffectImage.DOFade(0.55f, 0.08f))
            .SetUpdate(true);
    }

    void ApplySelectedState(bool instant)
    {
        SetGlowScale(Vector3.one);
        if (instant)
        {
            ScaleTarget.localScale = Vector3.one * 1.04f;
            SetGlowAlpha(0.85f);
        }
        else
        {
            hoverTween = ScaleTarget.DOScale(1.04f, 0.08f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        loopTween = DOTween.Sequence()
            .Append(glowEffectImage.transform.DOScale(1.025f, 0.75f).SetEase(Ease.InOutSine))
            .Join(glowEffectImage.DOFade(1.0f, 0.75f))
            .Append(glowEffectImage.transform.DOScale(1.0f, 0.75f).SetEase(Ease.InOutSine))
            .Join(glowEffectImage.DOFade(0.75f, 0.75f))
            .SetLoops(-1)
            .SetUpdate(true);
    }

    void SetGlowScale(Vector3 scale)
    {
        glowEffectImage.transform.localScale = scale;
    }

    void SetGlowAlpha(float alpha)
    {
        glowEffectImage.color = new Color(glowEffectImage.color.r, glowEffectImage.color.g, glowEffectImage.color.b, alpha);
    }

    void OnDestroy()
    {
        ResetState();
    }
}
