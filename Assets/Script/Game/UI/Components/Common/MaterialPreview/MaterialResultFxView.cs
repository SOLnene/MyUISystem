using DG.Tweening;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class MaterialResultFxView : MonoBehaviour
{
    [SerializeField] Image rotateCircleImage;
    [SerializeField] TextMeshProUGUI resultText;
    [SerializeField] AnimatedPanel resultTextPanel;
    [SerializeField] float rotateSpeed = 360f;
    [SerializeField] bool useUnscaledTime = true;

    RectTransform circleRect;
    Tween rotateTween;

    void Awake()
    {
        CacheReferences();
        Hide();
    }

    void OnDisable()
    {
        StopRotate();
    }

    void OnDestroy()
    {
        StopRotate();
    }

    public void ShowLoading()
    {
        CacheReferences();
        gameObject.SetActive(true);
        SetCircleVisible(true);
        HideTextRoot();
        StartRotate();
    }

    public void ShowMaxText(string text)
    {
        CacheReferences();
        gameObject.SetActive(true);
        StopRotate();
        SetCircleVisible(false);

        if (resultText != null)
        {
            resultText.text = text;
            ShowTextRoot();
        }
    }

    public void Hide()
    {
        StopRotate();
        SetCircleVisible(false);
        HideTextRoot();
        gameObject.SetActive(false);
    }

    void CacheReferences()
    {
        if (rotateCircleImage != null)
            circleRect = rotateCircleImage.rectTransform;
    }

    void StartRotate()
    {
        if (circleRect == null)
            return;

        StopRotate();
        circleRect.localRotation = Quaternion.identity;
        rotateTween = circleRect
            .DORotate(new Vector3(0f, 0f, -360f), rotateSpeed <= 0f ? 1f : 360f / rotateSpeed, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(useUnscaledTime);
    }

    void StopRotate()
    {
        if (rotateTween == null)
            return;

        rotateTween.Kill();
        rotateTween = null;
    }

    void SetCircleVisible(bool visible)
    {
        if (rotateCircleImage != null)
            rotateCircleImage.gameObject.SetActive(visible);
    }

    void SetTextVisible(bool visible)
    {
        if (resultText != null)
            resultText.gameObject.SetActive(visible);
    }

    void ShowTextRoot()
    {
        if (resultTextPanel != null)
        {
            resultTextPanel.Show().Forget();
            return;
        }

        SetTextVisible(true);
    }

    void HideTextRoot()
    {
        if (resultTextPanel != null)
        {
            resultTextPanel.HideImmediate();
            return;
        }

        SetTextVisible(false);
    }
}
