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

public class GachaMiddleView : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private Image equipIcon;

		#pragma warning restore 0649
#endregion

	CompositeDisposable disposable;
		
	public void Bind(GachaViewModel vm)
	{
		disposable = new CompositeDisposable();

		vm.CurrentPoolType
			.Subscribe(type =>
			{
				SwitchVisualAsync(type).Forget();
			}).AddTo(disposable);
	}

	async UniTask SwitchVisualAsync(GachaPoolType type)
	{
		var config = GameDatabase.GachaPoolUIConfigDatabase.Get(type);
		if (config == null) 
		{
			return;
		}
		var sprite =await ResourceManager.Instance.LoadAssetAsync<Sprite>(
			config.poolVisualPath);
		equipIcon.sprite = sprite;
	}
	
	void OnDisable()
	{
		disposable.Clear();
	}

	void OnDestroy()
	{
		disposable.Dispose();
	}
}
