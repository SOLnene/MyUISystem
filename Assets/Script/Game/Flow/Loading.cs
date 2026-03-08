using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
public class LoadingData
{
    public LoadingFunc loadingFunc;
    public bool isCleanupAsset = false;
}

public delegate IEnumerator LoadingFunc();
/// <summary>
/// 统一管理“加载流程 + Loading 界面 + 资源清理”的生命周期
/// </summary>
public class Loading : SingletonMono<Loading>
{
    LoadingData loadingData;
    Coroutine cor;

    private EventBinding<FinishLoadingEvent> finishBinding;

    void OnEnable()
    {
        finishBinding = new EventBinding<FinishLoadingEvent>(FinishLoading);
        EventBus<FinishLoadingEvent>.Register(finishBinding);
    }

    void OnDisable()
    {
        EventBus<FinishLoadingEvent>.Deregister(finishBinding);
    }
    
    public void StartLoading(LoadingFunc loadingFunc, bool isCleanupAsset = false)
    {
        // 打开 LoadingView
        UIManager.Instance.Open(UIType.UILoadingView);
        StartLoading(new LoadingData{loadingFunc = loadingFunc,isCleanupAsset = isCleanupAsset});
    }

    public void StartLoading(LoadingData data)
    {
        if (data.loadingFunc != null)
        {
            loadingData = data;
            if (cor != null)
            {
                StopCoroutine(cor);
            }
            cor = StartCoroutine(CorLoading());
        }
        else
        {
            Debug.LogError("加载错误,没有参数LoadingData！");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEnumerator CorLoading()
    {
        if (loadingData != null && loadingData.loadingFunc != null)
        {
            yield return StartCoroutine(loadingData.loadingFunc());
        }
        
        /*// 清理资源、GC
        yield return ResourceManager.Instance.CleanupAsync();
        
        Pool.ReleaseAll();
        yield return null;
        
        GC.Collect();
        yield return null;*/

        //FinishLoading();

    
    }
    
    /// <summary>
    /// 完成加载 
    /// </summary>
    public void FinishLoading()
    {
        UIManager.Instance.Close(UIType.UILoadingView, () =>
        {
            UIManager.Instance.Open(UIType.UIStartView);
        });
        CustomObjectPool<LoadingData>.Release(loadingData);
        loadingData = null;
        cor = null;
    }
}
