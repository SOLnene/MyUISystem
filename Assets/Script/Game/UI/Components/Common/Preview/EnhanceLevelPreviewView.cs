using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using TMPro;
using UniRx;
using Unity.VisualScripting;
using UnityEngine;
namespace  Game.UI.Components.CharacterDetail
{
	public class EnhanceLevelPreviewView : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI levelText;
		[SerializeField]
		private TextMeshProUGUI levelPlusText;
		[SerializeField]
		private TextMeshProUGUI expPlusAmoutText;
		[SerializeField]
		private TextMeshProUGUI expText;
		[SerializeField]
		private BarBase levelBar;
		[SerializeField]
		AnimatedPanel animatedPanel;

		CompositeDisposable disposable = new CompositeDisposable();
		EnhanceLevelPreviewViewModel Vm;


		public void Bind(EnhanceLevelPreviewViewModel viewModel)
		{
			disposable.Clear();
			Vm = viewModel;
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
					Debug.Log("levelup value" + value);
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
		
		public async UniTask Show()
		{
			gameObject.SetActive(true);

			if (animatedPanel != null)
				await animatedPanel.Show();
		}

		public async UniTask Hide()
		{
			if (animatedPanel != null)
			{
				await animatedPanel.Hide();
			}
			else
			{
				gameObject.SetActive(false);
			}
		}
		
		public void OnDestroy()
		{
			disposable.Dispose();
		}
	}
	
	
}
