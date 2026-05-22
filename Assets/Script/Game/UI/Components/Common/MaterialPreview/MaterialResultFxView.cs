using DG.Tweening;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class MaterialResultFxView : MonoBehaviour
{
    [SerializeField] GameObject content;
    [SerializeField] Image rotateCircleImage;
    [SerializeField] TextMeshProUGUI resultText;
    [SerializeField] AnimatedPanel resultTextPanel;
    [SerializeField] float rotateSpeed = 360f;
    [SerializeField] bool useUnscaledTime = true;
    
   
    Tween rotateTween;

    void Awake()
    {
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
        content.SetActive(true);
        rotateCircleImage.gameObject.SetActive(true);
        resultTextPanel.HideImmediate();
        StartRotate();
    }

    public void ShowMaxText(string text)
    {
        StopRotate();
        rotateCircleImage.gameObject.SetActive(false);
        content.SetActive(true);
        resultTextPanel.Show().Forget();
    }

    public void Hide()
    {
        StopRotate();
        rotateCircleImage.gameObject.SetActive(false);
        resultTextPanel.HideImmediate();
        content.SetActive(false);
    }

    void StartRotate()
    {
        StopRotate();
        rotateCircleImage.rectTransform.localRotation = Quaternion.identity;
        rotateTween = rotateCircleImage.rectTransform
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
}
