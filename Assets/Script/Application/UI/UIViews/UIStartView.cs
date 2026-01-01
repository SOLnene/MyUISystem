using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public partial class UIStartView : UIView
{
    //UIControlData
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        UIHelper.CreateFullScreenClick(this.transform, EnterGame, "FullScreenClick");
    }

    void EnterGame()
    {
        UIManager.Instance.Close(UIType.UIStartView, () =>
        {
            FlowManager.Instance.ActivateLoadedScene();
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
    }
}
