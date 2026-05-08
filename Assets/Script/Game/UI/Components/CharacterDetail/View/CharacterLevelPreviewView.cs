using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UniRx;
using Unity.VisualScripting;
using UnityEngine;
namespace  Game.UI.Components.CharacterDetail
{
	public class CharacterLevelPreviewView : BindableUI<EnhanceLevelPreviewViewModel>
	{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private TextMeshProUGUI levelText;
		[ControlBinding]
		private TextMeshProUGUI levelPlusText;
		[ControlBinding]
		private TextMeshProUGUI expPlusAmoutText;
		[ControlBinding]
		private TextMeshProUGUI expText;
		[ControlBinding]
		private BarBase levelBar;

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
					levelText.text = value;
				}).AddTo(disposable);
			
			Vm.expText.Subscribe(
				value =>
				{
					expText.text = value;
				}).AddTo(disposable);

			Vm.expPlusAmountText.Subscribe(
				value => {
					expPlusAmoutText.text = value;
				}).AddTo(disposable);
			
			Vm.levelUpText.Subscribe(
				value => {
					levelPlusText.text = value;
				}).AddTo(disposable);
			
			Observable.CombineLatest(Vm.expProgress, Vm.previewProgress,
					(current, preview) => (current, preview))
				.Subscribe(x =>
				{
					levelBar.SetValue(x.current, x.preview);
				})
				.AddTo(disposable);
			
			// 控制等级提升 (+N) 显示/隐藏
			Vm.isLevelChanged.Subscribe(isChanged =>
			{
				levelPlusText.gameObject.SetActive(isChanged);
			}).AddTo(disposable);

			// 控制经验增加 (+EXP) 显示/隐藏
			Vm.isExpAdding.Subscribe(isAdding =>
			{
				expPlusAmoutText.gameObject.SetActive(isAdding);
			}).AddTo(disposable);
					
		}
		
		public void OnDestroy()
		{
			disposable.Dispose();
		}
	}
	
	
}
