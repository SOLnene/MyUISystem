using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FlowManager : SingletonMono<FlowManager>
{

    AsyncOperation loadOp;
    
    EventBinding<EnterGameEvent> enterGameBinding;

    bool enterScene = false;
    
    /*void OnEnable()
    {
        enterGameBinding = new EventBinding<EnterGameEvent>(ActivateLoadedScene);
        EventBus<EnterGameEvent>.Register(enterGameBinding);
    }*/

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    

    public IEnumerator StartGameFlow()
    {
        // 1. 初始化资源管理器
        yield return ResourceManager.Instance.InitAsync();

        // 2. 初始化 UIManager
        yield return UIManager.Instance.InitUIConfig();

        // 3. 预加载 LoadingView
        yield return UIManager.Instance.PreLoad(UIType.UILoadingView);

        // 4. 启动 Loading，传入加载任务
        Loading.Instance.StartLoading(GameLoadingCoroutine);
    }
    
    private IEnumerator GameLoadingCoroutine()
    {
        // 1. 异步加载场景到 90%
        yield return LoadGameSceneAsync("Main");
        
        // 3. 等待用户点击继续
        yield return new WaitUntil(() => enterScene);

        // 4. 激活场景
        if (loadOp != null)
            loadOp.allowSceneActivation = true;

        // 5. 资源清理 & GC
        yield return ResourceManager.Instance.CleanupAsync();
        Pool.ReleaseAll();
        System.GC.Collect();
    }
    
    /// <summary>
    /// 异步加载场景
    /// </summary>
    private IEnumerator LoadGameSceneAsync(string sceneName)
    {
        loadOp = SceneManager.LoadSceneAsync(sceneName);
        loadOp.allowSceneActivation = false;
        //模拟
        yield return new WaitForSeconds(1.5f);
        while (!loadOp.isDone)
        {
            // 归一化进度
            float progress = Mathf.Clamp01(loadOp.progress / 0.9f);
            EventBus<LoadingProgressEvent>.Raise(new LoadingProgressEvent(progress, "加载场景中..."));

            if (loadOp.progress >= 0.9f)
            {
                EventBus<FinishLoadingEvent>.Raise(new FinishLoadingEvent());
                //Loading.Instance.FinishLoading(); // 关闭 LoadingView
                yield break; //
            }

            yield return null;
        }
    }
    
    /// <summary>
    /// 由 StartView 按钮触发，真正进入场景
    /// </summary>
    public void ActivateLoadedScene()
    {
        enterScene = true;
        Debug.Log("Enter Scene");
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Main")
        {
            Debug.Log("新场景加载完成: " + scene.name);
            // 在这里再打开 HubRoot
            UIManager.Instance.Open(UIType.HubRoot);
        }
    }
}
