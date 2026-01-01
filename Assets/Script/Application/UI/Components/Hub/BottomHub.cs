using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UnityEngine;

/// <summary>
/// 等级和血条
/// 如果没有复用或更复杂的逻辑的话可以直接作为uicontroldata让hubroot来赋值
/// 或许可以复用到别的血条上
/// </summary>
public class BottomHub : MonoBehaviour,IBindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private BarWithText barBg;
    [ControlBinding]
    private TextMeshProUGUI LevelText;

		#pragma warning restore 0649
#endregion
    
    //TODO:换用专有事件？
    EventBinding<PlayerStateEvent> binding;
    
    void OnEnable()
    {
        UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindDataTo(this);
        }
        binding = new EventBinding<PlayerStateEvent>(UpdateState);
        EventBus<PlayerStateEvent>.Register(binding);
    }

    public void UpdateState(PlayerStateEvent playerStateEvent)
    {
        LevelText.text = playerStateEvent.level.ToString();
        barBg.SetValue(playerStateEvent.hp, playerStateEvent.maxHp);
    }

    void OnDisable()
    {
        EventBus<PlayerStateEvent>.Deregister(binding);
    }
}
