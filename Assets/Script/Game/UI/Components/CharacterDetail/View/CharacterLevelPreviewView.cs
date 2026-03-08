using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
namespace  Game.UI.Components.CharacterDetail
{
	public class CharacterLevelPreviewView : BindableUI<CharacterLevelPreviewViewmodel>
	{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private TextMeshProUGUI levelValue;
		[ControlBinding]
		private TextMeshProUGUI expPlusAmoutText;
		[ControlBinding]
		private TextMeshProUGUI expValue;
		[ControlBinding]
		private EnhancePanelExpBar levelBar;

		#pragma warning restore 0649
#endregion

		CompositeDisposable disposable = new CompositeDisposable();
		//CharacterLevelPreviewViewmodel vm;
		public void BindData(UIControlData uiControlData)
		{
			if (uiControlData != null)
			{
				uiControlData.BindDataTo(this);
			}
		}

		public override void Bind(object data)
		{
			base.Bind(data);
			disposable.Clear();
			
			Vm.levelText.Subscribe(
				value =>
				{
					levelValue.text = value;
					Debug.Log($"level text update: {value}");
				}).AddTo(disposable);
			
			Vm.expText.Subscribe(
				value =>
				{
					expValue.text = value;
					Debug.Log($"exp text update: {value}");
				}).AddTo(disposable);

			Vm.expProgress.Subscribe(
				value =>
				{
					levelBar.SetValue(value);
					Debug.Log($"exp progress update: {value}");
				}).AddTo(disposable);
		}
		
		public void OnDestroy()
		{
			//disposable.Dispose();
		}
	}
	
	
}
