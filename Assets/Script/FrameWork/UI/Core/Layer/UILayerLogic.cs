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
}
