using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;

public class BindableUI : MonoBehaviour,IBindableUI
{
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
        
    }
    
    protected virtual void AfterBind()
    {
        
    }

    
}
public abstract class BindableUI<T> : BindableUI where T : class
{
    protected T Vm { get; private set; }

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
    }
}
