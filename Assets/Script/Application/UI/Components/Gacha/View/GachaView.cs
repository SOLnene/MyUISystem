using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;

/*GachaViewModel	应用层状态机（Application State）
GachaService	抽卡规则 / 领域逻辑（Domain）
GachaPoolProvider	数据来源 / 配置读取（Infrastructure）
GachaSession	一次抽卡过程（Transient Domain State）*/
public partial class GachaView : UIView
{
    GachaViewModel vm;
    CompositeDisposable disposable;
    [SerializeField]
    GachaPoolUIConfigDatabase poolUIConfigDatabase;
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
            vm = new GachaViewModel(GameContext.Instance.GachaService,
                GameContext.Instance.GachaVisualProvider);
        }
        disposable = new CompositeDisposable();
        
        //TopHub
        GachaTopHubViewModel topVm = new GachaTopHubViewModel(vm.CurrentPoolType);
        topHub.Bind(topVm).Forget();
        
        Draw1Btn.onClick.RemoveAllListeners();
        Draw10Btn.onClick.RemoveAllListeners();
        Draw1Btn.onClick.AddListener(() => vm.drawCommand.Execute(1));
        Draw10Btn.onClick.AddListener(() => vm.drawCommand.Execute(10));
        vm.OnSessionStarted
            .Subscribe(session =>
            {
                UIManager.Instance.Open(
                    UIType.GachaResultDetailPopup,
                    session
                    );
                //session在抽卡开始后才存在
                //todo:避免嵌套订阅
                session.OnPreviewFinished
                    .Subscribe(_ =>
                    {
                        UIManager.Instance.Close(UIType.GachaResultDetailPopup);
                        UIManager.Instance.Open(UIType.GachaResultPopup, vm.sessionVM);
                    })
                    .AddTo(this);
                session.OnSessionFinished
                    .Subscribe(_ =>
                    {
                        UIManager.Instance.Close(UIType.GachaResultPopup);
                        Debug.Log("抽卡会话结束");
                    })
                    .AddTo(this);
            })
            .AddTo(this);
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
