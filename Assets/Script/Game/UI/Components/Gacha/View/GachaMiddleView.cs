using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GachaMiddleView : BindableUI<GachaViewModel>
{
	CancellationTokenSource initCts;
	[SerializeField]
	AnimatedPanel anim;
	[SerializeField]
	Image Bg;
	[SerializeField]
	Image MainIcon;
	[SerializeField]
	Image SubIcon;
	[SerializeField]
	TMP_Text NameText;

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
		if (config.poolBackground == null || config.primaryRateUpIcon == null || config.secondaryRateUpIcon == null)
		{
			Debug.LogError($"加载失败: {config.gachaKey}");
			return;
		}
		Bg.sprite = config.poolBackground;
		MainIcon.sprite = config.primaryRateUpIcon;
		SubIcon.sprite = config.secondaryRateUpIcon;
		NameText.text = config.primaryRateUpName;
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
