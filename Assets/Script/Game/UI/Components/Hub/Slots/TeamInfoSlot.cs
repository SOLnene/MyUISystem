using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 只做显示
/// </summary>
public class TeamInfoSlot : BindableUI,IPointerClickHandler
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private TextMeshProUGUI IndexText;
    [ControlBinding]
    private Image CharaIcon;
    [ControlBinding]
    private BarBase bar;
    [ControlBinding]
    private TextMeshProUGUI CharaNameText;
    [ControlBinding]
    private Image ChargeIcon;
    [ControlBinding]
    private Image SelectedImage;

		#pragma warning restore 0649
#endregion

    PlayerStateEvent playerdata;
    /// <summary>
    /// 是否被选中
    /// </summary>
    bool isSelected;

    /// <summary>
    /// 由外部面板管理点击回调
    /// </summary>
    public Action onClick;
    
    
    /// <summary>
    /// 预留，之后会有更复杂的初始化（）
    /// </summary>
    /// <param name="slotIndex"></param>
    /// <param name="data"></param>
    public void Init(PlayerStateEvent data)
    {
        playerdata = data;
        UpdateVisualize();
    }

    public void SetData(PlayerStateEvent data)
    {
        playerdata = data;
        UpdateVisualize();
    }
    
    public void UpdateVisualize()
    {
        bar.SetValue(playerdata.hp, playerdata.maxHp);
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke();
    }

    /// <summary>
    /// 更新被选中显示
    /// </summary>
    /// <param name="selected"></param>
    public void SetSelect(bool selected)
    {
        if (selected)
        {
            SelectedImage.gameObject.SetActive(true);
        }
        else
        {
            SelectedImage.gameObject.SetActive(false);
        }
    }
    

}
