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
    		[ControlBinding]
    		private UIMotionBase motionRoot;
    
    		#pragma warning restore 0649
    #endregion
    

	CancellationTokenSource initCts;

	CompositeDisposable disposable = new CompositeDisposable();
	int requestId = 0;
	public override void Bind(GachaViewModel vm)
	{
		base.Bind(vm);
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
		motionRoot.Cancel();

		var loadTask = SwitchVisualAsync(type);
		var delayTask = UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: token);
		await UniTask.WhenAll(loadTask, delayTask);
		token.ThrowIfCancellationRequested();
		await motionRoot.PlayEnter();
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
	}
	
	void OnDisable()
	{
		motionRoot?.Cancel();
	}

	void OnDestroy()
	{
		initCts?.Cancel();
		motionRoot?.Cancel();
		disposable?.Dispose();
	}
}
