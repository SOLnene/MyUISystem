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
	bool isPresented;
	int requestVersion;
	internal bool IsReady { get; private set; }
	internal event Action<bool> InputLockChanged;

	internal async UniTask Show()
	{
		isPresented = true;
		await Init(viewModel.CurrentPoolConfig.Value);
	}

	internal async UniTask Hide()
	{
		CancelPresentation();
		await anim.Hide();
	}

	internal void CancelPresentation()
	{
		isPresented = false;
		IsReady = false;
		++requestVersion;
		var cts = initCts;
		initCts = null;
		cts?.Cancel();
		InputLockChanged?.Invoke(false);
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
				if (isPresented)
				{
					Init(config).Forget();
				}
			}).AddTo(disposable);

		//motionRoot.PlayEnter();
	}

	async UniTask Init(GachaPoolUIConfig config)
	{
		initCts?.Cancel();
		var cts = new CancellationTokenSource();
		initCts = cts;
		var token = cts.Token;
		int version = ++requestVersion;
		IsReady = false;
		InputLockChanged?.Invoke(true);
		try
		{
			if (config == null)
			{
				await anim.Hide(true);
				return;
			}
			// 三张内容图并行加载，与旧内容淡出并行；全部成功后一次性应用。
			var loadTask = UniTask.WhenAll(
				ResourceManager.Instance.LoadAssetAsync<Sprite>(config.poolBackgroundAddress, token),
				ResourceManager.Instance.LoadAssetAsync<Sprite>(config.primaryRateUpIconAddress, token),
				ResourceManager.Instance.LoadAssetAsync<Sprite>(config.secondaryRateUpIconAddress, token));
			var (sprites, _) = await UniTask.WhenAll(loadTask, anim.Hide(displayedPoolConfig == null));
			token.ThrowIfCancellationRequested();
			if (version != requestVersion || !isPresented)
			{
				return;
			}
			if (!SetPoolVisual(config, sprites.Item1, sprites.Item2, sprites.Item3))
			{
				return;
			}
			await anim.Show();
			token.ThrowIfCancellationRequested();
			IsReady = version == requestVersion && isPresented;
		}
		catch (OperationCanceledException)
		{
			// 取消的是本次等待，不释放 ResourceManager 共享缓存中的资源。
		}
		catch (Exception exception)
		{
			Debug.LogException(exception, this);
		}
		finally
		{
			// 旧请求不能解除新请求的输入限制，也不能在关闭后恢复交互。
			if (initCts == cts && version == requestVersion)
			{
				initCts = null;
				InputLockChanged?.Invoke(false);
			}
			cts.Dispose();
		}
	}

	bool SetPoolVisual(GachaPoolUIConfig config, Sprite background, Sprite primary, Sprite secondary)
	{
		if (background == null || primary == null || secondary == null)
		{
			Debug.LogError($"加载失败: {config.gachaKey}");
			return false;
		}
		Bg.sprite = background;
		MainIcon.sprite = primary;
		SubIcon.sprite = secondary;
		NameText.text = config.primaryRateUpName;
		displayedPoolConfig = config;
		return true;
	}
	
	void OnDisable()
	{
		CancelPresentation();
	}

	void OnDestroy()
	{
		CancelPresentation();
		disposable?.Dispose();
	}
}
