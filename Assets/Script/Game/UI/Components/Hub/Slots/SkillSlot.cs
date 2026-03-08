using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 目前用于测试
/// </summary>
public struct SkillData
{
	public string name;
	public Sprite icon;
	public KeyCode key;
	public float cooldown;   // 总冷却时间
	public bool available;   // 是否可用
}

public class SkillSlot : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	[ControlBinding]
	private Image SkillIcon;
	[ControlBinding]
	private Image CoolDownMask;
	[ControlBinding]
	private Image DisableMask;
	[ControlBinding]
	private TextMeshProUGUI SkillText;

		#pragma warning restore 0649
#endregion


    
	SkillData skillData;

	public override void OnEnable()
	{
		base.OnEnable();
		UpdateVisualize();
	}
	
	public void Init(SkillData data)
	{
		//防止init时未绑定
		UIControlData ctrlData = gameObject.GetComponent<UIControlData>();
		if(ctrlData != null)
		{
			ctrlData.BindDataTo(this);
		}
		skillData = data;
		UpdateVisualize();
	}
	
	public void SetData(SkillData data)
	{
		skillData = data;
	}
	
	public void UpdateVisualize()
	{
		//SetSkillIcon(skillData.icon);
		SetSkillText($"{skillData.key.ToString()}");
		SetCoolDown(skillData.cooldown,skillData.cooldown); // 初始满冷却
		SetAvailable(skillData.available);
	}
	
	public void SetSkillText(string text)
	{
		SkillText.text = text;
	}
	
	public void SetSkillIcon(Sprite icon)
	{
		SkillIcon.sprite = icon;
	}
	
	public void SetCoolDown(float coolDown,float maxCoolDown)
	{
		CoolDownMask.fillAmount = coolDown / maxCoolDown;
	}

	public void SetAvailable(bool available)
	{
		DisableMask.gameObject.SetActive(!available);
	}
}
