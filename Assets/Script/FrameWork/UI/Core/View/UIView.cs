using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;

/*
 * UI界面的显示和交互,所有具体视图的基类
 * 用于每个界面的root节点
 */
public class UIView : MonoBehaviour,IBindableUI
{
    UIViewHandle handle;
    //在此界面下选择的最后一个物体和默认选择物体
    GameObject lastSelect;
    public GameObject defaultSelect;
    
    //根节点的Canvas
    Canvas rootCanvas;
    public UIViewHandle Handle => handle;

    
    public virtual void OnInit(UIControlData uiControlData,UIViewHandle uiViewHandle)
    {
        if (uiControlData != null)
        {
            uiControlData.BindDataTo(this);
        }
        handle = uiViewHandle;
        rootCanvas = gameObject.GetOrAddComponent<Canvas>();
        gameObject.GetOrAddComponent<CanvasScaler>();
        gameObject.GetOrAddComponent<GraphicRaycaster>();
    }
    
    public virtual void OnAddListener()
    {
        
    }
    
    public virtual void OnRemoveListener()
    {
        
    }
    
    public virtual void OnOpen(object userData)
    {
        SortOrder(transform,handle.order+1);
        
        //todo；我关闭了这里的overridersorting,但这可能是我对框架的理解有误
        //rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = handle.order;
        
        OnAddListener();

        lastSelect = EventSystem.current.currentSelectedGameObject;
    }

    public virtual void OnResume()
    {
        if (defaultSelect != null)
        {
            EventSystem.current.SetSelectedGameObject(defaultSelect);
        }
    }
    
    public virtual void OnPause()
    {
        
    }
    
    public virtual void OnClose()
    {
        OnRemoveListener();
        if (lastSelect != null && lastSelect.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(lastSelect);
        }
    }

    public virtual void OnCancel()
    {
        UIManager.Instance.Close(handle.uiType);
    }
    
    public virtual void OnRelease()
    {
        
    }

    /// <summary>
    /// 递归设置物体与子物体的sortingorder
    /// </summary>
    /// <param name="target"></param>
    /// <param name="order"></param>
    /// <returns></returns>
    protected int SortOrder(Transform target, int order)
    {
        var canvas = target.GetComponent<Canvas>();
        if (canvas != null && canvas != rootCanvas)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = order++;
            canvas.gameObject.layer = LayerMask.NameToLayer("UI");
        }
        //粒子的显示
        var psr = target.GetComponent<ParticleSystemRenderer>();
        if (psr! != null)
        {
            psr.sortingOrder = order++;
            psr.gameObject.layer = LayerMask.NameToLayer("UI");
        }
        var sortGroup = target.GetComponent<SortingGroup>();
        if (sortGroup != null)
        {
            sortGroup.sortingOrder = order++;
            sortGroup.gameObject.SetLayerRecur(LayerMask.NameToLayer("UI"));
        }
        var sr =target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = order++;
            sr.gameObject.layer = LayerMask.NameToLayer("UI");
        }
        for (int i = 0; i < target.childCount; i++)
        {
            order = SortOrder(target.GetChild(i), order);
        }
        return order;
    }
}
