using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class StatItemView : BindableUI<StatItemViewModel>
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private Image icon;
    [ControlBinding]
    private TextMeshProUGUI label;
    [ControlBinding]
    private TextMeshProUGUI value;

		#pragma warning restore 0649
#endregion

    CompositeDisposable disposable = new CompositeDisposable();
    public override void Bind(object data)
    {
        base.Bind(data);
        disposable.Clear();
        icon.sprite = Vm.icon;
        label.text = Vm.label;
        Vm.valueText.Subscribe(text =>
        {
            value.text = text;
        }).AddTo(disposable);
    }

    public void OnDestroy()
    {
        disposable.Dispose();
    }
}
