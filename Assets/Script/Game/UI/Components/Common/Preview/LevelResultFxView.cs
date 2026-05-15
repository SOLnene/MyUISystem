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
    [SerializeField] float enterDuration = 0.24f;
    [SerializeField] float holdOldDuration = 0.16f;
    [SerializeField] float oldFadeDuration = 0.08f;
    [SerializeField] float newEnterDuration = 0.18f;
    [SerializeField] float holdNewDuration = 0.55f;
    [SerializeField] float exitDuration = 0.22f;
    [SerializeField] float newLevelStartScale = 1.35f;

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

    public async UniTask Play(int oldLevel, int newLevel, Color rarityColor)
    {
        if (canvasGroup == null || levelText == null)
            return;

        CacheDefaultPosition();
        Setup(oldLevel, rarityColor);
        await PlayEnter();
        await UniTask.Delay(System.TimeSpan.FromSeconds(holdOldDuration));
        await SwitchLevelText(newLevel);
        await UniTask.Delay(System.TimeSpan.FromSeconds(holdNewDuration));
        await PlayExit();

        levelText.rectTransform.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    void CacheDefaultPosition()
    {
        if (moveRoot == null || hasDefaultPos)
            return;

        defaultPos = moveRoot.anchoredPosition;
        hasDefaultPos = true;
    }

    void Setup(int oldLevel, Color rarityColor)
    {
        gameObject.SetActive(true);

        if (bgImage != null)
            bgImage.color = rarityColor;

        canvasGroup.alpha = 0f;
        levelText.text = $"Lv.{oldLevel}";
        levelText.alpha = 1f;
        levelText.rectTransform.localScale = Vector3.one;

        if (moveRoot != null)
            moveRoot.anchoredPosition = defaultPos + new Vector2(0f, 20f);
    }

    async UniTask PlayEnter()
    {
        var fadeTask = canvasGroup.DOFade(1f, enterDuration)
            .AsyncWaitForCompletion()
            .AsUniTask();

        if (moveRoot == null)
        {
            await fadeTask;
            return;
        }

        await UniTask.WhenAll(
            fadeTask,
            moveRoot.DOAnchorPos(defaultPos, enterDuration)
                .SetEase(Ease.OutCubic)
                .AsyncWaitForCompletion()
                .AsUniTask()
        );
    }

    async UniTask SwitchLevelText(int newLevel)
    {
        await DOTween.To(() => levelText.alpha, value => levelText.alpha = value, 0f, oldFadeDuration)
            .AsyncWaitForCompletion()
            .AsUniTask();

        levelText.text = $"Lv.{newLevel}";
        levelText.alpha = 0f;
        levelText.rectTransform.localScale = Vector3.one * newLevelStartScale;

        await UniTask.WhenAll(
            DOTween.To(() => levelText.alpha, value => levelText.alpha = value, 1f, newEnterDuration * 0.65f)
                .AsyncWaitForCompletion()
                .AsUniTask(),
            levelText.rectTransform
                .DOScale(1f, newEnterDuration)
                .SetEase(Ease.OutBack)
                .AsyncWaitForCompletion()
                .AsUniTask()
        );
    }

    async UniTask PlayExit()
    {
        await canvasGroup.DOFade(0f, exitDuration)
            .AsyncWaitForCompletion()
            .AsUniTask();
    }
}
