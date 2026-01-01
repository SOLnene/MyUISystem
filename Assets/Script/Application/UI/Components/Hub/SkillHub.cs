using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;

public class SkillHub : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private SkillSlot SkillSlot;

		#pragma warning restore 0649
#endregion

	
	
	public override void OnEnable()
	{
		base.OnEnable();
		SkillData fireball = new SkillData
		{
			name = "Fireball",
			icon = null,
			key = KeyCode.Q,
			cooldown = 5f,
			available = true
		};
		SkillSlot.SetData(fireball);
	}
	
	public void Init(SkillData data)
	{
		//SkillSlot.Init(data);
	}
	
	
}
