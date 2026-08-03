using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionButton : Button
{
    const float PressedHighlightAlpha = 0.2f;

    [SerializeField]
    CanvasGroup canvasGroup;
    [SerializeField]
    Color normalBgColor;
    [SerializeField]
    Color normalTextColor;
    [SerializeField]
    Color highlightedBgColor;
    [SerializeField]
    Color highlightedTextColor;
    [SerializeField]
    Color hoverFrameColor;
    [SerializeField]
    Image frame;
    [SerializeField]
    Image bg;
    [SerializeField]
    TextMeshProUGUI text;
    [SerializeField]
    Image highLightImage;
    [SerializeField, Range(0f, 1f)]
    float disabledAlpha = 0.5f;
    [SerializeField]
    float transitionDuration = 0.08f;
    [SerializeField]
    float pressedDuration = 0.25f;

    Tween visualTween;

    protected override void OnDisable()
    {
        visualTween?.Kill();
        base.OnDisable();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        visualTween?.Kill();

        if (instant)
        {
            ApplyStateInstant(state);
            return;
        }

        switch (state)
        {
            case SelectionState.Highlighted:
            case SelectionState.Selected:
                ShowHighlighted();
                break;
            case SelectionState.Pressed:
                ShowPressed();
                break;
            case SelectionState.Disabled:
                ShowDisabled();
                break;
            default:
                ShowNormal();
                break;
        }
    }

    void ApplyStateInstant(SelectionState state)
    {
        switch (state)
        {
            case SelectionState.Highlighted:
            case SelectionState.Selected:
                ShowHighlighted();
                break;
            case SelectionState.Pressed:
                SetPressedVisual();
                break;
            case SelectionState.Disabled:
                SetPressedVisual();
                canvasGroup.alpha = disabledAlpha;
                break;
            default:
                ShowNormal();
                break;
        }

        /*
        normal:
        frame.setactive(false);bg:normal,text:normal,highLightImage.setactive(false)
        hover:frameactivetrue,framecolor=hightlightcolor,
        press:hightlight dotween(1-0.2),0.25s?左右,bg highlight color,text highlightedTextColor
        pressing:我觉得应该是press状态的延伸,也就是保持highlight动画结束后的状态
        disable:pressing状态整体变淡。
         */
    }

    void ShowNormal()
    {
        canvasGroup.alpha = 1f;
        bg.color = normalBgColor;
        text.color = normalTextColor;
        frame.gameObject.SetActive(false);
        highLightImage.gameObject.SetActive(false);
    }

    void ShowHighlighted()
    {
        canvasGroup.alpha = 1f;
        bg.color = normalBgColor;
        text.color = normalTextColor;
        frame.gameObject.SetActive(true);
        frame.color = hoverFrameColor;
        highLightImage.gameObject.SetActive(false);
    }

    void ShowPressed()
    {
        canvasGroup.alpha = 1f;
        frame.gameObject.SetActive(true);
        frame.color = hoverFrameColor;
        highLightImage.gameObject.SetActive(true);

        Color highlightColor = highLightImage.color;
        highlightColor.a = 1f;
        highLightImage.color = highlightColor;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(bg.DOColor(highlightedBgColor, transitionDuration));
        sequence.Join(text.DOColor(highlightedTextColor, transitionDuration));
        sequence.Join(highLightImage.DOFade(PressedHighlightAlpha, pressedDuration));
        visualTween = sequence;
    }

    void ShowDisabled()
    {
        SetPressedVisual();
        visualTween = canvasGroup.DOFade(disabledAlpha, transitionDuration);
    }

    void SetPressedVisual()
    {
        canvasGroup.alpha = 1f;
        bg.color = highlightedBgColor;
        text.color = highlightedTextColor;
        frame.gameObject.SetActive(true);
        frame.color = hoverFrameColor;
        highLightImage.gameObject.SetActive(true);

        Color highlightColor = highLightImage.color;
        highlightColor.a = PressedHighlightAlpha;
        highLightImage.color = highlightColor;
    }
}
