using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hub右侧队伍信息面板
/// </summary>
public class RightHub : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private TeamInfoSlot[] TeamInfoSlot;

		#pragma warning restore 0649
	#endregion

	PlayerStateEvent[] teamData = new PlayerStateEvent[4];
	
	/// <summary>
	/// 选中了第几个角色
	/// 默认选中第一个
	/// 之后修改为保存的选中index
	/// </summary>
	int selectedIndex = 0;

	public override void OnEnable()
	{
		base.OnEnable();
	}
	
	public void Init(PlayerStateEvent[] datas)
	{
		for (int i = 0; i < TeamInfoSlot.Length; i++)
		{
			teamData[i] = datas[i];
			TeamInfoSlot[i].Init(teamData[i]);

			//防止闭包
			int index = i;
			TeamInfoSlot[i].onClick = () => OnSlotClick(index);
		}
	}

	public void UpDatePlayerData(int index, PlayerStateEvent data)
	{
		teamData[index] = data;
		TeamInfoSlot[index].SetData(data);
	}
	
	void OnSlotClick(int index)
	{
		selectedIndex = index;
		UpdateSelectedVisual();
		EventBus<PlayerStateEvent>.Raise(new PlayerStateEvent(teamData[selectedIndex]));
	}
	
	void UpdateSelectedVisual()
	{
		for (int i = 0; i < TeamInfoSlot.Length; i++)
		{
			TeamInfoSlot[i].SetSelect(i==selectedIndex);
		}
	}
}
