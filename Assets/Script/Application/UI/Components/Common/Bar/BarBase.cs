using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using DG.Tweening;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
public class BarBase : MonoBehaviour,IBindableUI,IBar
{
     #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    protected Image BarFill;

		#pragma warning restore 0649
#endregion
    

    public virtual void OnEnable()
    {
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
    }

    
    
    public virtual void BindData()
    {
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
    }
    
    public virtual void SetValue(int current, int max,int preview = -1)
    {
        if (max <= 0)
        {
            BarFill.fillAmount = 0;
            //text.text = "0/0";
            return;
        }
        float value = (float)current / max;
        BarFill.fillAmount = Mathf.Clamp01(value);
        //text.text = $"{currentHp}/{maxHp}";
    }
}
