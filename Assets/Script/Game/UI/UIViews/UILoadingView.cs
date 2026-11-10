using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Unity.VisualScripting;

public partial class UILoadingView : UIView
{
    EventBinding<LoadingProgressEvent> binding;
    [SerializeField] AnimatedPanel pagePanel;
    [SerializeField] Button cancelButton;
    [SerializeField] GameObject failurePanel;
    [SerializeField] TMP_Text failureText;
    [SerializeField] Button retryButton;
    [SerializeField] Button returnButton;
    [SerializeField] TMP_Text returnText;
    Tween progressTween;
    UniTaskCompletionSource<bool> retryChoice;

    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        binding = new EventBinding<LoadingProgressEvent>(SetVisualize);
        EventBus<LoadingProgressEvent>.Register(binding);
        Reset();
        ShowProgress();
        pagePanel.HideImmediate();
        ShowStartupAsync(data as StartupViewRequest).Forget(Debug.LogException);
    }

    async UniTask ShowStartupAsync(StartupViewRequest request)
    {
        try
        {
            await pagePanel.Show();
            request?.Complete(this);
        }
        catch (Exception error) { request?.Fail(error); }
    }

    internal void ShowProgress(bool allowCancel = true)
    {
        failurePanel.SetActive(false);
        cancelButton.gameObject.SetActive(allowCancel);
    }

    internal UniTask<bool> WaitForRetryAsync(string message, bool returningToGame, CancellationToken token)
    {
        failureText.text = message;
        returnText.text = returningToGame ? "返回游戏" : "返回登录";
        cancelButton.gameObject.SetActive(false);
        failurePanel.SetActive(true);
        retryButton.interactable = returnButton.interactable = true;
        retryChoice = new UniTaskCompletionSource<bool>();
        return retryChoice.Task.AttachExternalCancellation(token);
    }

    void Retry() => ResolveRetry(true);
    void Return() => ResolveRetry(false);
    void Cancel() => FlowManager.Instance.CancelEntry();

    void ResolveRetry(bool retry)
    {
        retryButton.interactable = returnButton.interactable = false;
        retryChoice?.TrySetResult(retry);
    }

    internal async UniTask HideStartupAsync()
    {
        await pagePanel.Hide();
    }

    public void SetVisualize(LoadingProgressEvent loadingProgressEvent)
    {
        progressTween?.Kill();
        if (loadingProgressEvent.Progress >= 1f)
        {
            // The exit animation may finish before the progress tween would reach the last icon.
            Silder.SetValueWithoutNotify(1f);
            progressTween = null;
        }
        else
        {
            progressTween = Silder.DOValue(loadingProgressEvent.Progress, 0.3f).SetUpdate(true);
        }
        TextDesc.text = loadingProgressEvent.Description;
    }
    
    public void Reset()
    {
#if UNITY_EDITOR
        // Unity also calls Reset when adding this component, before runtime UIControlData binding exists.
        if (!Application.isPlaying) return;
#endif
        Silder.SetValueWithoutNotify(0f);
        TextDesc.text = "正在准备载入…";
    }
    
    public override void OnAddListener()
    {
        base.OnAddListener();
        cancelButton.onClick.AddListener(Cancel);
        retryButton.onClick.AddListener(Retry);
        returnButton.onClick.AddListener(Return);
    }

    public override void OnRemoveListener()
    {
        cancelButton.onClick.RemoveListener(Cancel);
        retryButton.onClick.RemoveListener(Retry);
        returnButton.onClick.RemoveListener(Return);
        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        base.OnClose();
        EventBus<LoadingProgressEvent>.Deregister(binding);
        progressTween?.Kill();
        retryChoice?.TrySetCanceled();
        retryChoice = null;
        pagePanel.HideImmediate();
        Reset();
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
