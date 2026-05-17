using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ButtonBackgroundFader : MonoBehaviour
{
    [SerializeField] Graphic[] backgrounds;
    [SerializeField] float fadeDuration = 0.12f;
    [SerializeField] float staggerInterval = 0.02f;
    [SerializeField] bool useUnscaledTime = true;

    Tween fadeTween;

    public void Hide()
    {
        SetVisible(false);
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void SetVisible(bool visible, bool immediate = false)
    {
        KillTween();

        if (backgrounds == null)
            return;

        float targetAlpha = visible ? 1f : 0f;

        if (immediate)
        {
            foreach (var background in backgrounds)
                SetAlpha(background, targetAlpha);
            return;
        }

        var sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
        int index = 0;
        foreach (var background in backgrounds)
        {
            if (background == null)
                continue;

            sequence.Insert(index * staggerInterval, background.DOFade(targetAlpha, fadeDuration).SetEase(Ease.OutQuad));
            index++;
        }

        fadeTween = sequence;
    }

    void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    void KillTween()
    {
        if (fadeTween == null)
            return;

        fadeTween.Kill();
        fadeTween = null;
    }

    void OnDestroy()
    {
        KillTween();
    }
}
