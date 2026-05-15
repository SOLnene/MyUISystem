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
		private ProgressBarView levelBar;
		[SerializeField]
		AnimatedPanel animatedPanel;
		[SerializeField]
		LevelResultFxView levelResultFxView;
		CompositeDisposable disposable = new CompositeDisposable();
		EnhanceLevelPreviewViewModel Vm;
		const float expAnimationDuration = 0.8f;
		float currentExpProgress;
		bool isPlayingExpAnimation;
		int previewLevelUpCount;


		public void Bind(EnhanceLevelPreviewViewModel viewModel)
		{
			disposable.Clear();
			Vm = viewModel;
			currentExpProgress = 0f;
			isPlayingExpAnimation = false;
			previewLevelUpCount = 0;
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
			
			Vm.levelUpCount.Subscribe(count =>
			{
				previewLevelUpCount = count;
			}).AddTo(disposable);
			
			currentExpProgress = Vm.expProgress.Value;
			levelBar.SetValue(currentExpProgress, Vm.previewProgress.Value);
			
			Vm.expProgress.Skip(1).Subscribe(current =>
			{
				if (Mathf.Approximately(currentExpProgress, current))
				{
					return;
				}

				float from = currentExpProgress;
				currentExpProgress = current;
				PlayExpChange(from, current, previewLevelUpCount).Forget();
			}).AddTo(disposable);
			
			Vm.previewProgress.Skip(1).Subscribe(preview =>
			{
				if (isPlayingExpAnimation)
				{
					return;
				}

				levelBar.SetValue(currentExpProgress, preview);
			}).AddTo(disposable);
			
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
		
		public async UniTask PlayLevelResult(int oldLevel, int newLevel)
		{
			if (levelResultFxView == null || oldLevel == newLevel)
			{
				return;
			}
			
			await levelResultFxView.Play(oldLevel, newLevel);
		}
		
		async UniTask PlayExpChange(float from, float to, int fullSegmentCount)
		{
			isPlayingExpAnimation = true;
			try
			{
				await levelBar.PlaySegmentedValue(from, to, fullSegmentCount, expAnimationDuration);
			}
			finally
			{
				isPlayingExpAnimation = false;
				levelBar.SetValue(currentExpProgress, Vm.previewProgress.Value);
			}
		}
		
		public void OnDestroy()
		{
			disposable.Dispose();
		}
	}
	
	
}
