using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;


public partial class GachaResultDetailPopup : UIView
{
    //UIControlData
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private Button skipButton;
		[ControlBinding]
		private Button fullScreenButton;
		[ControlBinding]
		private GachaResultRevealView gachaResultRevealView;

		#pragma warning restore 0649
#endregion




    
    CompositeDisposable lifecycleDisposable = new CompositeDisposable();
    CompositeDisposable itemDisposable = new CompositeDisposable();
    
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        //TODO:或许该放到init中
        Bind(data as GachaSessionViewModel);
    }
    //todo:Bind() 必须是“可重复调用且无副作用的?
    public void Bind(GachaSessionViewModel viewModel)
    {
        lifecycleDisposable.Clear();
        var detailVM = new GachaResultDetailViewModel(viewModel);
        Debug.Log("绑定GachaResultDetailPopup，当前物品：" + detailVM.CurrentItem.Value?.Name);
        detailVM.CurrentItem.Subscribe(
             item =>
            {
                UpdateView(item);
            }).AddTo(lifecycleDisposable);
        
        skipButton.onClick.RemoveAllListeners();
        skipButton.onClick.AddListener(() =>
        {
            if (!gachaResultRevealView.IsRevealFinished())
            {
                gachaResultRevealView.SkipReveal();
            }
            else
            {
                detailVM.SkipCommand.Execute();
            }
        });
        //UIHelper.CreateFullScreenClick(transform, () => detailVM.NextCommand.Execute());
        fullScreenButton.onClick.RemoveAllListeners();
        fullScreenButton.onClick.AddListener(() =>
        {
            if (gachaResultRevealView.IsRevealFinished())
            {
                detailVM.NextCommand.Execute();
            }
            else
            {
                gachaResultRevealView.SkipReveal();
            }
        });
    }

    void UpdateView(GachaEntryViewModel entry)
    {
        // 清除旧订阅
        itemDisposable.Clear();
        //gachaResultRevealView.gameObject.SetActive(true);
        gachaResultRevealView.BindEntry(entry);
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
