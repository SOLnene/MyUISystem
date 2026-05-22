using DG.Tweening;
using UnityEngine;

public class BottomContentStateView : MonoBehaviour
{
    public enum State
    {
        Normal,
        Processing,
        Result
    }

    [SerializeField]
    GraphicStateVisual[] stateVisuals;
    [SerializeField]
    GameObject[] resultHiddenObjects;
    [SerializeField]
    CanvasGroup content;
    Sequence sequence;

    public void SetState(State state)
    {
        sequence?.Kill();
        sequence = null;
        content.gameObject.SetActive(true);
        switch (state)
        {
            case State.Normal:
                SetResultObjectsVisible(true);
                sequence = DOTween.Sequence().SetUpdate(true);
                AppendVisualState(GraphicStateVisual.State.Normal);
                break;
            case State.Processing:
                SetResultObjectsVisible(false);
                sequence = DOTween.Sequence().SetUpdate(true);
                AppendVisualState(GraphicStateVisual.State.Processing);
                break;
            case State.Result:
                content.gameObject.SetActive(false);
                break;
        }
    }

    public void SetStateInstant(State state)
    {
        sequence?.Kill();
        sequence = null;

        switch (state)
        {
            case State.Normal:
                SetResultObjectsVisible(true);
                SetVisualState(GraphicStateVisual.State.Normal);
                break;
            case State.Processing:
                SetResultObjectsVisible(true);
                SetVisualState(GraphicStateVisual.State.Processing);
                break;
            case State.Result:
                SetResultObjectsVisible(false);
                break;
        }
    }

    void AppendVisualState(GraphicStateVisual.State state)
    {
        for (int i = 0; i < stateVisuals.Length; i++)
            stateVisuals[i].AppendTo(sequence, state, 0f);
    }

    void SetVisualState(GraphicStateVisual.State state)
    {
        for (int i = 0; i < stateVisuals.Length; i++)
            stateVisuals[i].SetStateInstant(state);
    }

    void SetResultObjectsVisible(bool visible)
    {
        for (int i = 0; i < resultHiddenObjects.Length; i++)
            resultHiddenObjects[i].SetActive(visible);
    }

    void OnDestroy()
    {
        sequence?.Kill();
    }
}
