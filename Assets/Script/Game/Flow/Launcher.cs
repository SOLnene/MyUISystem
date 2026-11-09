using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 游戏的入口
/// </summary>
public class Launcher : MonoBehaviour
{
    [SerializeField] Camera startupCamera;
    [SerializeField] Light startupLight;
    [SerializeField] string worldScenePath;
    [SerializeField] GameObject startupStatus;
    [SerializeField] TMP_Text startupStatusText;
    [SerializeField] Button retryButton;
    bool starting;

    void Start()
    {
        retryButton.onClick.AddListener(RetryStartup);
        RunStartupAsync().Forget();
    }

    void RetryStartup() => RunStartupAsync().Forget();

    async UniTask RunStartupAsync()
    {
        if (starting) return;
        starting = true;
        startupStatus.SetActive(true);
        retryButton.interactable = false;
        startupStatusText.text = "正在初始化…";
        try
        {
            await FlowManager.Instance.BootAsync(startupCamera, startupLight, worldScenePath);
            if (this != null) startupStatus.SetActive(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (this == null) return;
            startupStatusText.text = "启动未完成，请重试。";
            retryButton.interactable = true;
            Debug.LogWarning("Startup initialization failed: " + error.Message);
        }
        finally { if (this != null) starting = false; }
    }

    void OnDestroy() => retryButton.onClick.RemoveListener(RetryStartup);

    IEnumerator StartGameFlow()
    {
        // 入口只负责调用流程管理器
        yield return RunStartupAsync().ToCoroutine();
    }
}
