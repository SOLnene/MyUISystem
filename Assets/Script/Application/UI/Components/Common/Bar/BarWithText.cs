using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BarWithText : BarBase
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    /*[ControlBinding]
    private Image HPBarFill;*/
    [ControlBinding]
    private TextMeshProUGUI HpText;

		#pragma warning restore 0649
#endregion



    public override void SetValue(int currentHp, int maxHp,int preview = -1)
    {
        base.SetValue(currentHp, maxHp);
        if (HpText != null)
        {
            HpText.text = $"{currentHp}/{maxHp}";
        }
    }
}
