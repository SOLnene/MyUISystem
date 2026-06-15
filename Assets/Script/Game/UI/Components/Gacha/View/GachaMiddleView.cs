using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UniRx;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class GachaMiddleView : BindableUI<GachaViewModel>
{
    #region 控件绑定变量声明，自动生成请勿手改
    		#pragma warning disable 0649
    		[ControlBinding]
    		private Image equipIcon;
    
    		#pragma warning restore 0649
    #endregion
    

	CancellationTokenSource initCts;
	[SerializeField]
	AnimatedPanel anim;

	CompositeDisposable disposable = new CompositeDisposable();
	GachaViewModel viewModel;
	GachaPoolUIConfig displayedPoolConfig;
	internal event Action<bool> InputLockChanged;

	internal async UniTask Show()
	{
		if (displayedPoolConfig != viewModel.CurrentPoolConfig.Value)
		{
			SetPoolVisual(viewModel.CurrentPoolConfig.Value);
		}

		await anim.Show();
	}

	internal async UniTask Hide()
	{
		initCts?.Cancel();
		await anim.Hide();
	}

	public override void Bind(GachaViewModel vm)
	{
		base.Bind(vm);
		viewModel = vm;
		disposable.Clear();

		vm.CurrentPoolConfig
			.DistinctUntilChanged()
			.Subscribe(config =>
			{
				Debug.Log($"PoolType 变化: {config}，触发 Init");
				Init(config).Forget();
			}).AddTo(disposable);

		//motionRoot.PlayEnter();
	}

	async UniTask Init(GachaPoolUIConfig config)
	{
		initCts?.Cancel();
		var cts = new CancellationTokenSource();
		initCts = cts;
		var token = cts.Token;

		if (displayedPoolConfig == null)
		{
			SetPoolVisual(config);
			return;
		}

		InputLockChanged?.Invoke(true);
		try
		{
			await anim.Hide();
			token.ThrowIfCancellationRequested();
			SetPoolVisual(config);
			await anim.Show();
		}
		finally
		{
			if (initCts == cts)
			{
				InputLockChanged?.Invoke(false);
			}
		}
	}

	void SetPoolVisual(GachaPoolUIConfig config)
	{
		if (config == null) 
		{
			return;
		}
		if (config.primaryRateUpIcon == null)
		{
			Debug.LogError($"加载失败: {config.gachaKey}");
			return;
		}
		equipIcon.sprite = config.primaryRateUpIcon;
		displayedPoolConfig = config;
	}
	
	void OnDisable()
	{
		initCts?.Cancel();
	}

	void OnDestroy()
	{
		initCts?.Cancel();
		disposable?.Dispose();
	}
}
