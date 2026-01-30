using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;


public partial class GachaResultDetailPopup : UIView
{
    //UIControlData
    CompositeDisposable lifecycleDisposable = new CompositeDisposable();
    CompositeDisposable itemDisposable = new CompositeDisposable();
    
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
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
        skipButton.onClick.AddListener(() => detailVM.SkipCommand.Execute());
        //UIHelper.CreateFullScreenClick(transform, () => detailVM.NextCommand.Execute());
        fullScreenButton.onClick.RemoveAllListeners();
        fullScreenButton.onClick.AddListener(() => detailVM.NextCommand.Execute());
    }

    void UpdateView(GachaEntryViewModel viewModel)
    {
        // 清除旧订阅
        itemDisposable.Clear();

        if (viewModel == null)
        {
            icon.sprite = null;
            nameText.text = string.Empty;
            return;
        }
        nameText.text = viewModel.Name;
        icon.sprite = viewModel.DetailImage;
        viewModel.OnVisualLoaded += () =>
        {
            icon.sprite = viewModel.DetailImage;
        };
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
