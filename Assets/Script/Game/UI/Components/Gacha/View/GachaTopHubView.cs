using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UniRx;
using UnityEngine;

public class GachaTopHubView : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	[ControlBinding]
	private RectTransform gachaPoolContent;

		#pragma warning restore 0649
#endregion



	GameObject tabPrefab;
	GachaTopHubViewModel vm;
	readonly List<GachaPoolTabView> tabViews = new List<GachaPoolTabView>();
	CancellationTokenSource bindCts;
	bool poolSwitchEnabled;
	readonly CompositeDisposable disposable = new CompositeDisposable();
	[SerializeField]
	AnimatedPanel anim;

	internal UniTask Show()
	{
		return anim.Show();
	}

	internal async UniTask Hide()
	{
		bindCts?.Cancel();
		await anim.Hide();
	}

	internal void SetPoolSwitchEnabled(bool enabled)
	{
		poolSwitchEnabled = enabled;
		foreach (var tab in tabViews)
		{
			tab.SetPoolSwitchEnabled(enabled);
		}
	}
	
	
	public async UniTask Bind(GachaTopHubViewModel viewModel)
	{
		Debug.Log("Bind Hub");
		if (ReferenceEquals(vm, viewModel) && tabViews.Count == viewModel.Tabs.Count)
		{
			return;
		}
		vm = viewModel;
		bindCts?.Cancel();
		var cts = new CancellationTokenSource();
		bindCts = cts;
		try
		{
			if (tabPrefab == null)
			{
				tabPrefab = await ResourceManager.Instance.LoadAssetAsync<GameObject>(
					"ui/gacha/gachapooltab", cts.Token);
			}
			// 缓存命中也可能同步返回；关闭后不能再创建页签。
			cts.Token.ThrowIfCancellationRequested();
			if (tabPrefab == null)
			{
				throw new InvalidOperationException("Gacha tab prefab could not be loaded.");
			}
			// 数量相同也可能换池或换顺序，必须绑定本次配置对应的标签。
			foreach (var tab in tabViews)
			{
				tab.gameObject.SetActive(false);
				Destroy(tab.gameObject);
			}
			tabViews.Clear();
			for (int i = 0; i < viewModel.Tabs.Count; i++)
			{
				var prefab = Instantiate(tabPrefab, gachaPoolContent);
				var tabView = prefab.GetComponent<GachaPoolTabView>();
				tabView.Bind(viewModel.Tabs[i], type =>
				{
					if (poolSwitchEnabled)
					{
						vm.SwitchPoolCommand.Execute(type);
					}
				});
				tabView.SetPoolSwitchEnabled(poolSwitchEnabled);
				tabViews.Add(tabView);
			}
		}
		finally
		{
			if (bindCts == cts)
			{
				bindCts = null;
			}
			cts.Dispose();
		}
	}

	void OnDisable()
	{
		bindCts?.Cancel();
	}

	void OnDestroy()
	{
		bindCts?.Cancel();
		disposable.Dispose();
	}
}
