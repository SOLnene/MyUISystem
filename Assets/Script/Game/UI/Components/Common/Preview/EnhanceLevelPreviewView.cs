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


		public void Bind(EnhanceLevelPreviewViewModel viewModel)
		{
			disposable.Clear();
			Vm = viewModel;
			currentExpProgress = 0f;
			isPlayingExpAnimation = false;
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
	
			Refresh();
			
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
		
		public async UniTask PlayLevelResult(int oldLevel, int newLevel, Color rarityColor)
		{
			if (levelResultFxView == null || oldLevel == newLevel)
			{
				return;
			}
			
			await levelResultFxView.Play(oldLevel, newLevel, rarityColor);
		}
		
		public void Refresh()
		{
			if (Vm == null || levelBar == null)
			{
				return;
			}

			currentExpProgress = Vm.expProgress.Value;
			levelBar.SetValue(currentExpProgress, Vm.previewProgress.Value);
		}
		
		public async UniTask PlayExpProgress(EnhanceResultData result)
		{
			isPlayingExpAnimation = true;
			try
			{
				if (levelBar != null)
				{
					await levelBar.PlaySegmentedValue(result.oldProgress, result.newProgress, result.levelUpCount, expAnimationDuration);
				}

				currentExpProgress = result.newProgress;
				if (levelBar != null)
				{
					levelBar.SetValue(currentExpProgress, Vm.previewProgress.Value);
				}
			}
			finally
			{
				isPlayingExpAnimation = false;
			}
		}
		
		public async UniTask PlayLevelResult(EnhanceResultData result)
		{
			await PlayLevelResult(result.oldLevel, result.newLevel, result.rarityColor);
		}
		
		public void OnDestroy()
		{
			disposable.Dispose();
		}
	}
	
	
}
