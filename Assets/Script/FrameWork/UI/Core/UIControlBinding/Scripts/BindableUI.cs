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
    }
}
