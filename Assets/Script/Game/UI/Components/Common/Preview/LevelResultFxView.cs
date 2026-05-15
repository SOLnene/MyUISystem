using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelResultFxView : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform moveRoot;
    [SerializeField] TextMeshProUGUI levelText;
    [SerializeField] Image bgImage;
    [SerializeField] float enterDuration = 0.18f;
    [SerializeField] float holdOldDuration = 0.12f;
    [SerializeField] float switchDuration = 0.16f;
    [SerializeField] float exitDuration = 0.15f;

    Vector2 defaultPos;
    bool hasDefaultPos;

    void Awake()
    {
        if (moveRoot != null)
        {
            defaultPos = moveRoot.anchoredPosition;
            hasDefaultPos = true;
        }

        gameObject.SetActive(false);
    }

    public async UniTask Play(int oldLevel, int newLevel)
    {
        if (canvasGroup == null || levelText == null)
            return;

        if (moveRoot != null && !hasDefaultPos)
        {
            defaultPos = moveRoot.anchoredPosition;
            hasDefaultPos = true;
        }

        gameObject.SetActive(true);

        canvasGroup.alpha = 0f;
        if (moveRoot != null)
            moveRoot.anchoredPosition = defaultPos + new Vector2(0f, 20f);

        levelText.text = $"Lv.{oldLevel}";

        if (moveRoot != null)
        {
            await UniTask.WhenAll(
                canvasGroup.DOFade(1f, enterDuration).AsyncWaitForCompletion().AsUniTask(),
                moveRoot.DOAnchorPos(defaultPos, enterDuration)
                    .SetEase(Ease.OutCubic)
                    .AsyncWaitForCompletion()
                    .AsUniTask()
                );
        }
        else
        {
            await canvasGroup.DOFade(1f, enterDuration).AsyncWaitForCompletion().AsUniTask();
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(holdOldDuration));

        levelText.text = $"Lv.{newLevel}";

        if (moveRoot != null)
        {
            await moveRoot.DOAnchorPos(defaultPos + new Vector2(0f, -8f), switchDuration * 0.45f)
                .SetEase(Ease.OutQuad)
                .AsyncWaitForCompletion()
                .AsUniTask();

            await moveRoot.DOAnchorPos(defaultPos, switchDuration * 0.55f)
                .SetEase(Ease.OutBack)
                .AsyncWaitForCompletion()
                .AsUniTask();
        }

        await canvasGroup.DOFade(0f, exitDuration)
            .AsyncWaitForCompletion()
            .AsUniTask();

        gameObject.SetActive(false);
    }
}
