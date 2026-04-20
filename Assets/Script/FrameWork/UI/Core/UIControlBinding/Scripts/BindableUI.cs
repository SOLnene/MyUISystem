using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;

public class BindableUI : MonoBehaviour,IBindableUI
{
    //因为有很多之前的类没走bindable，所有onenable还不能删
    public virtual void OnEnable()
    {
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
        AfterBind();
    }

    public virtual void Bind(object data)
    {
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
    }
    
    protected virtual void AfterBind()
    {
        
    }

    
}
public abstract class BindableUI<T> : BindableUI where T : class
{
    protected T Vm { get; private set; }

    /// <summary>
    /// 防报错
    /// </summary>
    /// <param name="data"></param>
    public override void Bind(object data)
    {
        if (data == null)
        {
            return;
        }
        Vm = data as T;
            
        if (Vm == null && data != null)
        {
            UnityEngine.Debug.LogWarning($"[UI] {name} 类型转换失败: 期望 {typeof(T).Name}");
        }
        //因为有很多之前的类没走这里，所有onenable还不能删
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
    }
    
    //现在没用
    public virtual void Bind(T data)
    {
        if (data == null)
        {
            return;
        }
        Vm = data;
            
        if (Vm == null && data != null)
        {
            UnityEngine.Debug.LogWarning($"[UI] {name} 类型转换失败: 期望 {typeof(T).Name}");
        }
    }
}
