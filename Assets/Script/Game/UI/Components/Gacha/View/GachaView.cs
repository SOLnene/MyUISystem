using System;
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
    bool isOpening;
    int presentationVersion;
    GachaTopHubViewModel topVm;
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
        isInputLocked = false;
        disposable.Clear();
        vm.OnSessionStarted
            .Subscribe(flowController.StartSession)
            .AddTo(disposable);
        ShowHubs(++presentationVersion).Forget();
    }

    void Bind()
    {
        if (vm == null)
        {
            vm = new GachaViewModel(GameContext.Instance.GachaService,
                GameContext.Instance.GachaVisualProvider,
                poolUIConfigDatabase);
        }
        disposable = new CompositeDisposable();
        
        //TopHub
        topVm = new GachaTopHubViewModel(
            vm.PoolConfigs,
            vm.CurrentPoolConfig,
            vm.SwitchPool);
        // 加载时仍允许关闭，改为分别控制切池与抽卡按钮，不用全屏挡板。
        inputBlocker.SetActive(false);
        //middleView
        middleHub.InputLockChanged -= SetInputLocked;
        middleHub.InputLockChanged += SetInputLocked;
        middleHub.Bind(vm);
        
        Draw1Btn.onClick.RemoveAllListeners();
        Draw10Btn.onClick.RemoveAllListeners();
        Draw1Btn.onClick.AddListener(() =>
        {
            if (!isClosing && !isOpening && !isInputLocked && middleHub.IsReady)
            {
                vm.drawCommand.Execute(1);
            }
        });
        Draw10Btn.onClick.AddListener(() =>
        {
            if (!isClosing && !isOpening && !isInputLocked && middleHub.IsReady)
            {
                vm.drawCommand.Execute(10);
            }
        });
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(OnCancel);
    }

    async UniTask ShowHubs(int version)
    {
        isOpening = true;
        ApplyInputState();
        try
        {
            await UniTask.WhenAll(topHub.Bind(topVm), topHub.Show(), middleHub.Show(), bottomHub.Show());
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // 关闭或重开会改变版本；旧的打开流程不得恢复新页面的输入。
            if (version == presentationVersion && !isClosing)
            {
                isOpening = false;
                ApplyInputState();
            }
        }
    }

    async UniTask CloseWithAnimation()
    {
        int version = presentationVersion;
        ApplyInputState();
        try
        {
            await UniTask.WhenAll(topHub.Hide(), middleHub.Hide(), bottomHub.Hide());
            if (version == presentationVersion && isClosing)
            {
                base.OnCancel();
            }
        }
        finally
        {
            if (version == presentationVersion)
            {
                ApplyInputState();
            }
        }
    }

    void SetInputLocked(bool isLocked)
    {
        isInputLocked = isLocked;
        ApplyInputState();
    }

    void ApplyInputState()
    {
        bool canSwitch = !isClosing && !isOpening && !isInputLocked;
        topHub.SetPoolSwitchEnabled(canSwitch);
        // Transient draw locks are checked in click callbacks without changing button visuals.
        closeBtn.interactable = !isClosing;
    }

    public override void OnCancel()
    {
        if (isClosing)
        {
            return;
        }

        isClosing = true;
        ++presentationVersion;
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
        ++presentationVersion;
        isClosing = true;
        isOpening = false;
        middleHub.CancelPresentation();
        flowController.CancelSession();
        disposable.Clear();
        base.OnClose();
    }

    public override void OnRelease()
    {
        middleHub.InputLockChanged -= SetInputLocked;
        disposable.Dispose();
        topVm.Dispose();
        flowController.Dispose();
        base.OnRelease();
    }
}
