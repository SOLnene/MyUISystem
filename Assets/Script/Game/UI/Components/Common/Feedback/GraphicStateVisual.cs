using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

//按钮等组件，默认动画中一部分变透明，一部分变白
public class GraphicStateVisual : MonoBehaviour
{
    public enum State
    {
        Normal,
        Processing
    }

    //动画过程中变暗的部分
    [SerializeField]
    Graphic[] backgrounds;
    [SerializeField]
    //动画过程中变亮的部分
    Graphic[] foregrounds;
    [SerializeField]
    Color processingBackgroundColor = new Color(1f, 1f, 1f, 0);
    [SerializeField]
    Color processingForegroundColor = new Color32(255, 255, 255, 255);
    [SerializeField]
    float fadeDuration = 0.12f;

    Color[] normalBackgroundColors;
    Color[] normalForegroundColors;
    bool hasNormalState;

    public void AppendTo(Sequence sequence, State state, float at)
    {
        CaptureNormalState();

        for (int i = 0; i < backgrounds.Length; i++)
        {
            var targetColor = state == State.Normal ? normalBackgroundColors[i] : processingBackgroundColor;
            sequence.Insert(at, backgrounds[i].DOColor(targetColor, fadeDuration).SetEase(Ease.OutQuad));
        }

        for (int i = 0; i < foregrounds.Length; i++)
        {
            var targetColor = state == State.Normal ? normalForegroundColors[i] : processingForegroundColor;
            sequence.Insert(at, foregrounds[i].DOColor(targetColor, fadeDuration).SetEase(Ease.OutQuad));
        }
    }

    public void SetStateInstant(State state)
    {
        CaptureNormalState();

        for (int i = 0; i < backgrounds.Length; i++)
            backgrounds[i].color = state == State.Normal ? normalBackgroundColors[i] : processingBackgroundColor;

        for (int i = 0; i < foregrounds.Length; i++)
            foregrounds[i].color = state == State.Normal ? normalForegroundColors[i] : processingForegroundColor;
    }

    void CaptureNormalState()
    {
        if (hasNormalState)
            return;

        normalBackgroundColors = new Color[backgrounds.Length];
        for (int i = 0; i < backgrounds.Length; i++)
            normalBackgroundColors[i] = backgrounds[i].color;

        normalForegroundColors = new Color[foregrounds.Length];
        for (int i = 0; i < foregrounds.Length; i++)
            normalForegroundColors[i] = foregrounds[i].color;

        hasNormalState = true;
    }
}
