using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//管理同一层 UI 的打开顺序、暂停/恢复和渲染顺序
public class UILayerLogic
{
    public UILayer layer;
    public Canvas canvas;
    int maxOrder;
    HashSet<int> orders;
    public Stack<UIViewHandle> openedViewHandles;

    public UILayerLogic(UILayer uiLayer, Canvas canvas)
    {
        this.layer = uiLayer;
        this.canvas = canvas;
        maxOrder = (int)uiLayer;
        orders = new HashSet<int>();
        openedViewHandles = new Stack<UIViewHandle>();
    }

    public void CloseUI(UIViewHandle closedUI)
    {
        int order = closedUI.order;
        ReleaseOrderAndRemove(closedUI);
        closedUI.order = 0;

        if (openedViewHandles.Count > 0)
        {
            var topViewHandle = openedViewHandles.Peek();
            // 拿到最上层UI，如果被暂停的话，则恢复，
            if (topViewHandle != null && topViewHandle.isPause)
            {
                topViewHandle.isPause = false;
                if (topViewHandle.uiView != null)
                {
                    topViewHandle.uiView.OnResume();
                }
            }
            //更新在这个ui下面的非窗口ui的topViewNum
            if (!closedUI.isWindow)
            {
                foreach (var handle in openedViewHandles)
                {
                    if (handle != closedUI
                    && handle.IsOpen() 
                    && handle.order < order)
                    {
                        handle.AddTopViewNum(-1);
                    }
                }
            }
        }
    }

    public void OpenUI(UIViewHandle openedUI)
    {
        //新打开
        if (openedUI.order == 0)
        {
            openedUI.order = AllocateOrderAndPush(openedUI);
        }
        foreach (var viewHandle in openedViewHandles)
        {
            if (viewHandle != openedUI
                && viewHandle.IsOpen()
                && viewHandle.order < openedUI.order
                && viewHandle.uiView != null)
            {
                if (!viewHandle.isPause)
                {
                    viewHandle.isPause = true;
                    viewHandle.uiView.OnPause();
                }
                if (!openedUI.isWindow)
                {
                    viewHandle.AddTopViewNum(1);
                }
            }
        }
    }

    public void ReleaseOrderAndRemove(UIViewHandle closedUI)
    {
        int order = closedUI.order;
        if(orders.Remove(order))
        {
            maxOrder = (int)layer;
            foreach (var item in orders)
            {
                maxOrder = Mathf.Max(maxOrder, item);
            }
        }

        List<UIViewHandle> list = ListPool<UIViewHandle>.Get();
        while (openedViewHandles.Count > 0)
        {
            var view = openedViewHandles.Pop();
            if (view != closedUI)
            {
                list.Add(view);
            }
            else
            {
                break;
            }
        }
        for(int i=list.Count - 1; i >= 0; i--)
        {
            openedViewHandles.Push(list[i]);
        }
        ListPool<UIViewHandle>.Release(list);
    }

    public int AllocateOrderAndPush(UIViewHandle viewHandle)
    {
        maxOrder += 10;
        orders.Add(maxOrder);
        openedViewHandles.Push(viewHandle);
        return maxOrder;

    }
}
