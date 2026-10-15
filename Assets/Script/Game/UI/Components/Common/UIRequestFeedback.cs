using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class UIRequestFeedback : MonoBehaviour
{
    [SerializeField] GameObject blockingRoot;
    [SerializeField] RectTransform spinner;
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text detailText;
    [SerializeField] Button cancelButton;

    Func<bool> isPending;
    Action cancel;
    float elapsed;

    void Awake() => cancelButton.onClick.AddListener(CancelWaiting);

    void CancelWaiting() => cancel?.Invoke();

    internal void ShowProcessing(string message, Func<bool> pending = null, Action cancelAction = null)
    {
        isPending = pending;
        cancel = cancelAction;
        cancelButton.gameObject.SetActive(cancelAction != null);
        elapsed = 0f;
        statusText.text = message;
        detailText.text = "请稍候";
        spinner.localRotation = Quaternion.identity;
        blockingRoot.SetActive(true);
    }

    internal void HideProcessing()
    {
        isPending = null;
        cancel = null;
        elapsed = 0f;
        blockingRoot.SetActive(false);
    }

    void Update()
    {
        // 观察实际请求状态，页面重新打开后也不会留下失效的遮罩。
        if (isPending != null && !isPending())
        {
            HideProcessing();
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        spinner.Rotate(0f, 0f, -180f * Time.unscaledDeltaTime);
        if (elapsed >= 8f)
        {
            // 长等待仍由调用方决定结束流程，取消按钮只退出本次等待。
            detailText.text = "请求耗时较长，请稍候。";
        }
    }

    void OnDisable()
    {
        isPending = null;
        cancel = null;
        elapsed = 0f;
    }
}
