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
    public GachaViewModel vm;
    CompositeDisposable disposable;
    GachaFlowController flowController;
    [SerializeField]
    GachaPoolUIConfigDatabase poolUIConfigDatabase;
    [SerializeField]
    Button closeBtn;
    [SerializeField]
    AnimatedPanel bottomHub;
    [SerializeField]
    GameObject inputBlocker;
    bool isClosing;
    bool isInputLocked;
    //UIControlData
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
        flowController = new GachaFlowController();
        Bind();
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        isClosing = false;
        disposable.Clear();
        vm.OnSessionStarted
            .Subscribe(flowController.StartSession)
            .AddTo(disposable);
        ShowHubs().Forget();
    }

    void Bind()
    {
        if (vm == null)
        {
            vm = new GachaViewModel(GameContext.Instance.GachaService,
                GameContext.Instance.GachaVisualProvider,
                poolUIConfigDatabase.Configs[0]);
        }
        disposable = new CompositeDisposable();
        
        //TopHub
        GachaTopHubViewModel topVm = new GachaTopHubViewModel(
            vm.CurrentPoolConfig,
            poolUIConfigDatabase.Configs);
        topHub.Bind(topVm).Forget();
        //middleView
        middleHub.InputLockChanged -= SetInputLocked;
        middleHub.InputLockChanged += SetInputLocked;
        middleHub.Bind(vm);
        
        Draw1Btn.onClick.RemoveAllListeners();
        Draw10Btn.onClick.RemoveAllListeners();
        Draw1Btn.onClick.AddListener(() => vm.drawCommand.Execute(1));
        Draw10Btn.onClick.AddListener(() => vm.drawCommand.Execute(10));
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(OnCancel);
    }

    async UniTask ShowHubs()
    {
        SetInputLocked(true);
        try
        {
            await UniTask.WhenAll(topHub.Show(), middleHub.Show(), bottomHub.Show());
        }
        finally
        {
            SetInputLocked(false);
        }
    }

    async UniTask CloseWithAnimation()
    {
        SetInputLocked(true);
        try
        {
            await UniTask.WhenAll(topHub.Hide(), middleHub.Hide(), bottomHub.Hide());
            base.OnCancel();
        }
        finally
        {
            SetInputLocked(false);
        }
    }

    void SetInputLocked(bool isLocked)
    {
        isInputLocked = isLocked;
        inputBlocker.SetActive(isInputLocked);
    }

    public override void OnCancel()
    {
        if (isClosing)
        {
            return;
        }

        isClosing = true;
        CloseWithAnimation().Forget();
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
        flowController.CancelSession();
        disposable.Clear();
        base.OnClose();
    }

    public override void OnRelease()
    {
        middleHub.InputLockChanged -= SetInputLocked;
        disposable.Dispose();
        flowController.Dispose();
        base.OnRelease();
    }
}
