using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;


public partial class HubRoot : UIView
{
    //UIControlData
    
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
    }

    /// <summary>
    /// 主界面测试
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            UpdatePlayerStats();
        }
    }

    public void UpdatePlayerStats()
    {
        int maxHp = Random.Range(0, 10000);
        PlayerStateEvent playerStateEvent = new PlayerStateEvent
        {
            characterId = Random.Range(0,4),
            maxHp = maxHp,
            hp = Random.Range(0,maxHp),
            level = Random.Range(1,100),
            charge = Random.Range(0,1)
        };
        EventBus<PlayerStateEvent>.Raise(playerStateEvent);
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
