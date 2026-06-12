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
	GachaPoolType? displayedPoolType;
	internal event Action<bool> InputLockChanged;

	internal async UniTask Show()
	{
		if (!displayedPoolType.HasValue || displayedPoolType.Value != viewModel.CurrentPoolType.Value)
		{
			SetPoolVisual(viewModel.CurrentPoolType.Value);
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

		vm.CurrentPoolType
			.DistinctUntilChanged()
			.Subscribe(type =>
			{
				Debug.Log($"PoolType 变化: {type}，触发 Init");
				Init(type).Forget();
			}).AddTo(disposable);

		//motionRoot.PlayEnter();
	}

	async UniTask Init(GachaPoolType type)
	{
		initCts?.Cancel();
		var cts = new CancellationTokenSource();
		initCts = cts;
		var token = cts.Token;

		if (!displayedPoolType.HasValue)
		{
			SetPoolVisual(type);
			return;
		}

		InputLockChanged?.Invoke(true);
		try
		{
			await anim.Hide();
			token.ThrowIfCancellationRequested();
			SetPoolVisual(type);
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

	void SetPoolVisual(GachaPoolType type)
	{
		var config = GameDatabase.GachaPoolUIConfigDatabase.Get(type);
		if (config == null) 
		{
			return;
		}
		if (config.poolVisual == null)
		{
			Debug.LogError($"加载失败: {type}");
			return;
		}
		equipIcon.sprite = config.poolVisual;
		displayedPoolType = type;
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
