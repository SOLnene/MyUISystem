using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;


public partial class CharacterDetailView : UIView
{
    //UIControlData
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private RectTransform topBarLayer;
		[ControlBinding]
		private RectTransform contentLayer;
		[ControlBinding]
		private RectTransform bottomLayer;
		[ControlBinding]
		private TextMeshProUGUI charaText;
		[ControlBinding]
		private CharacterDetailContentView contentView;
		[ControlBinding]
		private Image final;

		#pragma warning restore 0649
#endregion
















    
    private const float TOP_BAR_HEIGHT = 150f;     // 根据设计稿改
    private const float BOTTOM_BAR_HEIGHT = 140f;

    CharacterDetailViewModel vm;
    CompositeDisposable disposable = new CompositeDisposable();
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        vm = data as CharacterDetailViewModel;
        Bind(vm);
    }

    public void Bind(CharacterDetailViewModel viewModel)
    {
        disposable.Clear();
        viewModel.model.Name
            .Subscribe(name =>
            {
                charaText.text = name;
            }).AddTo(disposable);
        final.gameObject.SetActive(false);
        var contentVm = new CharacterDetailContentViewModel(viewModel.model); 
        contentView.Bind(contentVm);
    }
    
    public override void OnAddListener()
    {
        base.OnAddListener();
    }

    public override void OnRemoveListener()
    {
        base.OnRemoveListener();
    }

    public override void OnClose()
    {
        base.OnClose();
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
