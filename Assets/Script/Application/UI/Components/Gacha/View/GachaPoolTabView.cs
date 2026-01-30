using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GachaPoolTabView : BindableUI
{
	  #region 控件绑定变量声明，自动生成请勿手改

			#pragma warning disable 0649
		[ControlBinding]
		private Image selectBg;
		[ControlBinding]
		private Image normalBg;
		[ControlBinding]
		private Image icon;
		[ControlBinding]
		private Button btn;

			#pragma warning restore 0649
		#endregion

	GachaPoolTabViewModel vm;
	CompositeDisposable disposable = new CompositeDisposable();
	public void Bind(GachaPoolTabViewModel viewModel,Action<GachaPoolType> onClick)
	{
		vm = viewModel;
		var isSelected = viewModel.IsSelected.Value;
		
		viewModel.IsSelected
			.Subscribe(b =>
			{
				UpdateSelected(b);
			}).AddTo(disposable);

		icon.sprite = GameDatabase.GachaPoolUIConfigDatabase.Get(viewModel.PoolType).tabIcon;
        
		btn.onClick.RemoveAllListeners();
		btn.onClick.AddListener(()=>onClick.Invoke(vm.PoolType));
	}
	
	void UpdateSelected(bool selected)
	{
		selectBg.gameObject.SetActive(selected);
		normalBg.gameObject.SetActive(!selected);
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
