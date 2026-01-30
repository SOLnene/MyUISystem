using System.Collections;
using System.Collections.Generic;
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
	readonly CompositeDisposable disposable = new CompositeDisposable();
	
	
	public async UniTask Bind(GachaTopHubViewModel viewModel)
	{
		vm = viewModel;
		if (tabPrefab == null)
		{
			tabPrefab = await ResourceManager.Instance.LoadAssetAsync<GameObject>("ui/gacha/gachapooltab"); 
		}
		foreach (var tab in viewModel.Tabs)
		{
			var prefab = Instantiate(tabPrefab, gachaPoolContent);
			var tabView = prefab.GetComponent<GachaPoolTabView>();
			tabView.Bind(tab, type => viewModel.SwitchPoolCommand.Execute(type));
		}
		
	}
}
