using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public partial class TutorialOverlayView : UIView
{
    [SerializeField] RectTransform pageRoot;
    [SerializeField] RectTransform fullScreenDimmer;
    [SerializeField] RectTransform cutoutDimmerRoot;
    [SerializeField] RectTransform topBlocker;
    [SerializeField] RectTransform bottomBlocker;
    [SerializeField] RectTransform leftBlocker;
    [SerializeField] RectTransform rightBlocker;
    [SerializeField] RectTransform focusRoot;
    [SerializeField] RectTransform focusFrame;
    [SerializeField] RectTransform convergingFrame;
    [SerializeField] RectTransform messagePanel;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] float focusPadding = 16f;
    [SerializeField] float convergingStartPadding = 120f;
    [SerializeField] float convergingDuration = 0.45f;

    ITutorialOverlaySession session;
    RectTransform currentTarget;
    Tween convergingTween;
    Rect convergingStartRect;
    float convergingProgress;

    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        session = data as ITutorialOverlaySession;
        if (session == null)
        {
            Debug.LogError("TutorialOverlayView requires a tutorial overlay session.", this);
            return;
        }

        session.Attach(this);
    }

    public override void OnAddListener()
    {
        base.OnAddListener();
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        currentTarget = null;
        StopConvergingAnimation();
        session?.Detach(this);
        session = null;
        base.OnClose();
    }

    public override void OnRelease()
    {
        StopConvergingAnimation();
        base.OnRelease();
    }

    internal void ShowMessageOnly(string message)
    {
        currentTarget = null;
        StopConvergingAnimation();
        messageText.text = message;
        fullScreenDimmer.gameObject.SetActive(true);
        cutoutDimmerRoot.gameObject.SetActive(false);
        focusRoot.gameObject.SetActive(false);
        messagePanel.gameObject.SetActive(true);
    }

    internal void FocusTarget(RectTransform target, string message)
    {
        StopConvergingAnimation();
        currentTarget = target;
        messageText.text = message;
        fullScreenDimmer.gameObject.SetActive(false);
        cutoutDimmerRoot.gameObject.SetActive(true);
        focusRoot.gameObject.SetActive(true);
        messagePanel.gameObject.SetActive(true);
        PlayConvergingAnimation(UpdateFocusRect());
    }

    internal void HideGuidance()
    {
        currentTarget = null;
        StopConvergingAnimation();
        fullScreenDimmer.gameObject.SetActive(false);
        cutoutDimmerRoot.gameObject.SetActive(false);
        focusRoot.gameObject.SetActive(false);
        messagePanel.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            session?.SkipForTesting();
        }
    }
#endif

    void LateUpdate()
    {
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            Rect focusRect = UpdateFocusRect();
            if (convergingTween != null && convergingTween.IsActive())
            {
                // 插值位置和尺寸而非缩放节点，避免九宫格边线随动画变粗或变细。
                SetRect(convergingFrame, LerpRect(convergingStartRect, focusRect, convergingProgress));
            }
        }
    }

    Rect UpdateFocusRect()
    {
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            pageRoot,
            currentTarget);
        Rect parentRect = pageRoot.rect;
        float minX = Mathf.Clamp(bounds.min.x - focusPadding, parentRect.xMin, parentRect.xMax);
        float maxX = Mathf.Clamp(bounds.max.x + focusPadding, parentRect.xMin, parentRect.xMax);
        float minY = Mathf.Clamp(bounds.min.y - focusPadding, parentRect.yMin, parentRect.yMax);
        float maxY = Mathf.Clamp(bounds.max.y + focusPadding, parentRect.yMin, parentRect.yMax);

        SetRect(bottomBlocker, parentRect.xMin, parentRect.yMin, parentRect.xMax, minY);
        SetRect(topBlocker, parentRect.xMin, maxY, parentRect.xMax, parentRect.yMax);
        SetRect(leftBlocker, parentRect.xMin, minY, minX, maxY);
        SetRect(rightBlocker, maxX, minY, parentRect.xMax, maxY);
        SetRect(focusFrame, minX, minY, maxX, maxY);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    void PlayConvergingAnimation(Rect focusRect)
    {
        Rect parentRect = pageRoot.rect;
        convergingStartRect = Rect.MinMaxRect(
            Mathf.Max(parentRect.xMin, focusRect.xMin - convergingStartPadding),
            Mathf.Max(parentRect.yMin, focusRect.yMin - convergingStartPadding),
            Mathf.Min(parentRect.xMax, focusRect.xMax + convergingStartPadding),
            Mathf.Min(parentRect.yMax, focusRect.yMax + convergingStartPadding));
        convergingProgress = 0f;
        convergingFrame.gameObject.SetActive(true);
        SetRect(convergingFrame, convergingStartRect);

        // 教程可能覆盖暂停时间的界面，因此收束动画不依赖游戏 timeScale。
        convergingTween = DOTween.To(
                () => convergingProgress,
                value => convergingProgress = value,
                1f,
                convergingDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetTarget(convergingFrame)
            .OnComplete(() =>
            {
                convergingTween = null;
                convergingFrame.gameObject.SetActive(false);
            });
    }

    void StopConvergingAnimation()
    {
        convergingTween?.Kill();
        convergingTween = null;
        convergingFrame.gameObject.SetActive(false);
    }

    void SetRect(RectTransform rectTransform, Rect rect)
    {
        SetRect(rectTransform, rect.xMin, rect.yMin, rect.xMax, rect.yMax);
    }

    Rect LerpRect(Rect from, Rect to, float progress)
    {
        return Rect.MinMaxRect(
            Mathf.Lerp(from.xMin, to.xMin, progress),
            Mathf.Lerp(from.yMin, to.yMin, progress),
            Mathf.Lerp(from.xMax, to.xMax, progress),
            Mathf.Lerp(from.yMax, to.yMax, progress));
    }

    void SetRect(RectTransform rectTransform, float minX, float minY, float maxX, float maxY)
    {
        Rect parentRect = pageRoot.rect;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        rectTransform.anchoredPosition = new Vector2(
            minX - parentRect.xMin,
            minY - parentRect.yMin);
        rectTransform.sizeDelta = new Vector2(
            Mathf.Max(0f, maxX - minX),
            Mathf.Max(0f, maxY - minY));
    }
}
