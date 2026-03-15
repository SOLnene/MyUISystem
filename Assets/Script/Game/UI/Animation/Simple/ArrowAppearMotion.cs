using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 用于升级箭头的淡入和移动动画，直接挂到Image上即可
[RequireComponent(typeof(Image))]
public class ArrowAppearMotion : MonoBehaviour
{
    [Header("动画设置")]
    public float fadeDuration = 0.3f;        // 淡入时间
    public float moveDistance = -20f;         // 移动距离(像素/单位)
    public float moveDuration = 0.5f;        // 移动花费的时间
    public float delayBefore = 0f;           // 动画启动延迟

    private CanvasGroup group;
    RectTransform rectTransform;
    private Vector2 originalAnchoredPos;
    private Tween appearTween;

    void Awake()
    {
        // 获取Image用于透明度控制（若要支持 RawImage 也可做成泛型）
        group = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        // 初始化透明度
        group.alpha = 0;
        rectTransform.anchoredPosition = Vector2.zero + new Vector2(0f, moveDistance);
        appearTween?.Kill();
        
        appearTween = DOTween.Sequence()
            .AppendInterval(delayBefore)
            .Append(group.DOFade(1, fadeDuration).SetEase(Ease.OutQuad))
            .Join(transform.DOLocalMove(Vector3.zero, moveDuration).SetEase(Ease.OutQuad));
    }

    void OnDisable()
    {
        appearTween?.Kill();
    }
    
}
