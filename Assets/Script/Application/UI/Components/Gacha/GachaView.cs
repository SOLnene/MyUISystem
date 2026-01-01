using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;


public partial class GachaView : UIView
{
    GachaViewModel vm;
    CompositeDisposable disposable;
    
    //UIControlData
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
        Bind();
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
    }

    void Bind()
    {
        if (vm == null)
        {
            vm = new GachaViewModel();
        }
        disposable = new CompositeDisposable();
        Draw1Btn.onClick.RemoveAllListeners();
        Draw10Btn.onClick.RemoveAllListeners();
        Draw1Btn.onClick.AddListener(() => vm.drawCommand.Execute(1));
        Draw10Btn.onClick.AddListener(() => vm.drawCommand.Execute(10));
        vm.lastDrawnItems.ObserveAdd().Subscribe(add =>
        {
            //这里可以添加抽到物品时的UI反馈逻辑
            Debug.Log($"抽到物品: {add.Value.Name}");
        }).AddTo(disposable);
        vm.currentIndex.Subscribe(index =>
        {
            if (index < 0 || index >= vm.lastDrawnItems.Count)
            {
                return;
            }
            singleResultPopup.gameObject.SetActive(true);
            singleResultPopup.Bind(vm.singleResultVM);
        }).AddTo(disposable);
        
        UIHelper.CreateFullScreenClick(singleResultPopup.transform, () =>
        {
            vm.ShowNextOrClose();
            singleResultPopup.gameObject.SetActive(vm.hasNext.Value);
            Debug.Log("点击全屏，显示下一个或关闭");
        });
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
        disposable.Dispose();
    }
}
