using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GachaPoolTabView : BindableUI
{
	  #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	[SerializeField]
	private Image selectBg;
	[SerializeField]
	private Image normalBg;
	[SerializeField]
	private Image icon;
	[SerializeField]
	private Button btn;
	[SerializeField]
	private GachaTabFeedback content;

		#pragma warning restore 0649
#endregion



	GachaPoolTabViewModel vm;
	CompositeDisposable disposable = new CompositeDisposable();
	CancellationTokenSource iconCts;
	public void Bind(GachaPoolTabViewModel viewModel,Action<GachaPoolUIConfig> onClick)
	{
		vm = viewModel;
		disposable.Clear();
		
		viewModel.IsSelected
			.Subscribe(b =>
			{
				UpdateSelected(b);
			}).AddTo(disposable);

		if (isActiveAndEnabled)
		{
			LoadIcon();
		}
        
		btn.onClick.RemoveAllListeners();
		btn.onClick.AddListener(()=>onClick.Invoke(vm.Config));
	}

	internal void SetPoolSwitchEnabled(bool enabled)
	{
		btn.interactable = enabled;
	}

	void LoadIcon()
	{
		iconCts?.Cancel();
		iconCts?.Dispose();
		iconCts = new CancellationTokenSource();
		IconLoader.SetSpriteAsync(icon, vm.IconAddress, iconCts.Token).Forget();
	}

	public override void OnEnable()
	{
		// 控件已由 prefab 序列化绑定，不调用基类的 UIControlData 反射绑定。
		// 页签会随动画根节点隐藏；再次显示时重试被取消的图标加载。
		if (vm != null)
		{
			LoadIcon();
		}
	}
	
	protected override void AfterBind()
	{
		base.AfterBind();
	}
	
	void UpdateSelected(bool selected)
	{
		content.SetSelected(selected);
	}
	void OnDisable()
	{
		iconCts?.Cancel();
	}

	void OnDestroy()
	{
		iconCts?.Cancel();
		iconCts?.Dispose();
		disposable.Dispose();
	}
}
