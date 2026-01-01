using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;

public class EnhancePanelExpBar : BarBase
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	[ControlBinding]
	protected Image BarPreviewFill;

		#pragma warning restore 0649
#endregion



		#pragma warning restore 0649
#endregion

	public override void SetValue(int current, int max, int preview = -1)
	{
	    base.SetValue(current, max);
	    if (BarPreviewFill != null && preview >= 0)
	    {
		    BarPreviewFill.fillAmount = Mathf.Clamp01((float)preview / max);
	    }
	}
}
