using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum UIState
{
    Closed,
    Opening,
    Opened,
    Closing
}

/*负责某个 UI 界面（UIView） 的

加载（Load/Preload）

打开（Open）

关闭（Close）

销毁（Release）

状态维护（是否打开、在哪个层级、动画状态等）*/
public class UIViewHandle
{
    public UIType uiType;
    public string address;
    public bool isWindow;
    public Type uiViewType;
    
    public UIView uiView;

    public UIAnimator uiAnimator;
    
    public UIState uiState = UIState.Closed;
    
    //记录期望目标状态
    //TODO:可能需要改成枚举（打开、关闭、暂停、恢复）
    public bool shouldOpen = false;
    
    public bool isLoading = false;

    public UILayerLogic uiLayerLogic;

    public int order;

    public bool isPause;
    
    /// <summary>
    /// 在我上面的界面(非窗口界面)的数量
    /// </summary>
    public int topViewNum;
    
    //todo:自定义管理类，不再暴露AsyncOperationHandle
    public AsyncOperationHandle Load(object data = null, Action callback = null)
    {
        isLoading = true;
        if (shouldOpen)
        {
            uiLayerLogic.AllocateOrderAndPush(this);
        }
        return ResourceManager.Instance.InstantiateAsync(address, (go) =>
        {
            //如果加载完成时用户已经取消（isLoading == false），立刻回收并退出
            if (!isLoading)
            {
                ResourceManager.Instance.Recycle(go);
                callback?.Invoke();
                Release();
                return;
            }

            isLoading = false;
            /*
             * GetOrAddComponent<UIView>()
            👉 编译时写死 = 一定是 UIView。

            (UIView) GetOrAddComponent(uiViewType)
            👉 运行时决定实际类型 = 可能是 UILoadingView，只是用基类引用来接。
             */
            uiView = (UIView)go.GetOrAddComponent(uiViewType);
            if (isWindow)
            {
                uiView.transform.SetParent(uiLayerLogic.canvas.transform, false);
            }
            else
            {
                uiView.transform.SetParentAndReset(uiLayerLogic.canvas.transform);

                RectTransform rectTransform = uiView.transform as RectTransform;
                rectTransform.SetAnchor(AnchorPresets.StretchAll);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.sizeDelta = Vector2.zero;
            }
            
            uiView.OnInit(go.GetComponent<UIControlData>(),this);
            uiView.transform.SetAsLastSibling();

            if (shouldOpen)
            {
                Show(data,callback,true);
            }

            else
            {
                Close(callback);
            }

        });
        
    }
    
    public void Show(object data = null,Action callback = null,bool firstOpen = false)
    {
        shouldOpen = true;
        //防止重复加载
        if (isLoading)
        {
            return;
        }

        if (uiState == UIState.Opened || uiState == UIState.Opening)
        {
            if (uiView != null && uiView.RefreshWhenAlreadyOpen)
            {
                uiView.OnOpen(data);
                callback?.Invoke();
            }

            return;
        }
        
        if(uiView == null)
        {
            Load(data, callback);
        }
        else
        {
            if(!firstOpen && shouldOpen)
            {
                InternalClose(callback);
            }
            InternalOpen(data);
        }
      
        
    }

    public void Close(Action callback = null)
    {
        shouldOpen = false;
        if (isLoading)
        {
            return;
        }
        if (uiState == UIState.Closed || uiState == UIState.Closing)
            return; // 已经关闭或正在关闭
        
        if (uiView != null)
        {
            InternalClose(callback);
        }
    }
    
    public void Release()
    {
        if (uiView != null)
        {
            if (shouldOpen)
            {
                InternalClose();
            }
            uiView.OnRelease();
            GameObject.Destroy(uiView.gameObject);
        }
        uiView = null;
        isLoading = false;
        shouldOpen = false;
    }

    /// <summary>
    /// 
    /// </summary>
    public void InternalOpen(object data = null, Action callback = null)
    {
        uiLayerLogic.OpenUI(this);
        uiState = UIState.Opening;
        //Todo:层级管理
        SetVisible(true);
        //Todo:调用uiview内部方法
        
        uiView.OnOpen(data);
        if (uiAnimator == null)
        {
            uiAnimator = uiView.GetComponent<UIAnimator>();
        }
        if(uiAnimator != null)
        {
            UIAnimationManager.Instance.RegisterAnimator(uiAnimator);
            uiAnimator.PlayGroup("Open", ()=>
            {
                uiState = UIState.Opened;
                callback?.Invoke();
            });
        }
        //回调放到动画结束后
        //callback?.Invoke();
    }

    public void InternalClose(Action callback = null)
    {
        uiLayerLogic.CloseUI(this);
        uiState = UIState.Closing;
        //Todo:层级管理
        
        //Todo:调用uiview内部方法
        if (uiAnimator == null)
        {
            uiAnimator = uiView.GetComponent<UIAnimator>();
        }
        if (uiAnimator != null)
        {
            UIAnimationManager.Instance.UnregisterAnimator(uiAnimator);
            uiAnimator.PlayGroup("Close", () =>
            {
                uiView.OnClose();
                SetVisible(false);
                uiState = UIState.Closed;
                callback?.Invoke(); 
            });
        }
        else
        {
            uiView.OnClose();
            SetVisible(false);
            uiState = UIState.Closed;
            callback?.Invoke(); 
        }
    }

    /// <summary>
    /// 刷新界面显示,可能不需要
    /// </summary>
    /// <param name="data"></param>
    /// <param name="callback"></param>
    public void InternalRefresh(object data = null,Action callback = null)
    {
        SetVisible(true);
        uiView.OnOpen(data);
        callback?.Invoke();
    }
    
    public void SetVisible(bool visible)
    {
        if (uiView != null)
        {
            uiView.gameObject.SetActive(visible);
        }            
    }

    public bool IsOpen()
    {
        return uiState == UIState.Opening || uiState == UIState.Opened;
    }

    public void AddTopViewNum(int num)
    {
        topViewNum += num;
        topViewNum = Mathf.Max(0, topViewNum);
        SetVisible(topViewNum<=0);
    }
}
