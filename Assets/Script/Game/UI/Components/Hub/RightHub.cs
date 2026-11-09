using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hub右侧队伍信息面板
/// </summary>
public class RightHub : BindableUI
{
    [Serializable]
    sealed class SidePortrait
    {
        [SerializeField] internal string characterKey;
        [SerializeField] internal Sprite sprite;
    }

    [SerializeField] SidePortrait[] sidePortraits = Array.Empty<SidePortrait>();

    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private TeamInfoSlot[] TeamInfoSlot;

		#pragma warning restore 0649
	#endregion

	PlayerStateEvent[] teamData = new PlayerStateEvent[4];
	readonly CompositeDisposable teamSubscriptions = new();
	HubTeamViewModel teamViewModel;
	[SerializeField] TMP_Text emptyTeamLabel;
	
	/// <summary>
	/// 选中了第几个角色
	/// 默认选中第一个
	/// 之后修改为保存的选中index
	/// </summary>
	int selectedIndex = 0;

	public override void OnEnable()
	{
		base.OnEnable();
		if (teamViewModel != null) BindModel(teamViewModel);
	}

	internal void BindModel(HubTeamViewModel viewModel)
	{
		base.Bind(viewModel);
		teamSubscriptions.Clear();
		teamViewModel = viewModel;
		emptyTeamLabel.gameObject.SetActive(false);
		if (viewModel == null)
		{
			foreach (var slot in TeamInfoSlot) slot.BindCharacter(null, 0);
			return;
		}
		viewModel.Revision.Subscribe(_ =>
		{
			bool hasMember = false;
			for (int i = 0; i < TeamInfoSlot.Length; i++)
			{
				int index = i;
				TeamInfoSlot[i].onClick = () => OnSlotClick(index);
				var member = viewModel.Members[i];
				TeamInfoSlot[i].BindCharacter(member, i,
					member != null ? FindSidePortrait(member.Definition.key) : null);
				hasMember |= viewModel.Members[i] != null;
			}
			emptyTeamLabel.gameObject.SetActive(!hasMember);
			UpdateSelectedVisual();
		}).AddTo(teamSubscriptions);
        viewModel.ActiveCharacter.Subscribe(_ => UpdateSelectedVisual()).AddTo(teamSubscriptions);
	}

	Sprite FindSidePortrait(string characterKey)
	{
		// These sprites are dependencies of the HUD prefab, so they share its resource lifetime.
		foreach (var portrait in sidePortraits)
			if (portrait.characterKey == characterKey) return portrait.sprite;
		return null;
	}

	void OnDisable()
	{
		teamSubscriptions.Clear();
	}

	void OnDestroy()
	{
		teamSubscriptions.Dispose();
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
		if (teamViewModel != null)
		{
			teamViewModel.Select(index);
			return;
		}
		selectedIndex = index;
		UpdateSelectedVisual();
		EventBus<PlayerStateEvent>.Raise(new PlayerStateEvent(teamData[selectedIndex]));
	}
	
	void UpdateSelectedVisual()
	{
		for (int i = 0; i < TeamInfoSlot.Length; i++)
		{
			bool active = teamViewModel != null
				? teamViewModel.Members[i] == teamViewModel.ActiveCharacter.Value
				: i == selectedIndex;
			TeamInfoSlot[i].SetSelect(active);
		}
	}
}
