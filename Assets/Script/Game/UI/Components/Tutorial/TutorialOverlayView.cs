using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] RectTransform messagePanel;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] float focusPadding = 16f;

    ITutorialOverlaySession session;
    RectTransform currentTarget;

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
        session?.Detach(this);
        session = null;
        base.OnClose();
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }

    internal void ShowMessageOnly(string message)
    {
        currentTarget = null;
        messageText.text = message;
        fullScreenDimmer.gameObject.SetActive(true);
        cutoutDimmerRoot.gameObject.SetActive(false);
        focusRoot.gameObject.SetActive(false);
        messagePanel.gameObject.SetActive(true);
    }

    internal void FocusTarget(RectTransform target, string message)
    {
        currentTarget = target;
        messageText.text = message;
        fullScreenDimmer.gameObject.SetActive(false);
        cutoutDimmerRoot.gameObject.SetActive(true);
        focusRoot.gameObject.SetActive(true);
        messagePanel.gameObject.SetActive(true);
        UpdateFocusRect();
    }

    void LateUpdate()
    {
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
        {
            UpdateFocusRect();
        }
    }

    void UpdateFocusRect()
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
