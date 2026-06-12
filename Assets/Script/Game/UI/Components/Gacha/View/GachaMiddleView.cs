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
	int requestId = 0;
	GachaViewModel viewModel;
	GachaPoolType? displayedPoolType;

	internal async UniTask Show()
	{
		if (!displayedPoolType.HasValue || displayedPoolType.Value != viewModel.CurrentPoolType.Value)
		{
			await SwitchVisualAsync(viewModel.CurrentPoolType.Value);
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

	/// <summary>
	/// 给一个最小时间，避免image没加载出来的卡顿
	/// </summary>
	/// <param name="type"></param>
	async UniTask Init(GachaPoolType type)
	{
		initCts?.Cancel();
		initCts = new CancellationTokenSource();
		var token = initCts.Token;

		if (!displayedPoolType.HasValue)
		{
			await SwitchVisualAsync(type);
			token.ThrowIfCancellationRequested();
			return;
		}

		await anim.Hide();
		token.ThrowIfCancellationRequested();

		var loadTask = SwitchVisualAsync(type);
		var delayTask = UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);
		await UniTask.WhenAll(loadTask, delayTask);
		token.ThrowIfCancellationRequested();
		await anim.Show();
	}

	async UniTask SwitchVisualAsync(GachaPoolType type)
	{
		Debug.Log("SwitchVisualAsync " + type);
		int id = ++requestId;
		var config = GameDatabase.GachaPoolUIConfigDatabase.Get(type);
		if (config == null) 
		{
			return;
		}
		var sprite =await ResourceManager.Instance.LoadAssetAsync<Sprite>(
			config.poolVisualPath);
		if (sprite == null)
		{
			Debug.LogError($"加载失败: {config.poolVisualPath}");
			return;
		}
		if (id != requestId)
		{
			return;
		}
		equipIcon.sprite = sprite;
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
