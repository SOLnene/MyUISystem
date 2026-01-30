using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
public class GachaResultItemView : BindableUI
{
    CompositeDisposable disposable = new CompositeDisposable();

    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private Image icon;

		#pragma warning restore 0649
#endregion
    
    public void Bind(GachaEntryViewModel viewModel)
    {
        disposable.Clear();
        icon.sprite = viewModel.Icon;
    }

    public void OnDestroy()
    {
        disposable.Dispose();
    }
}
