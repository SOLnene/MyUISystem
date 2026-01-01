using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Unity.VisualScripting;

public partial class UILoadingView : UIView
{
    EventBinding<LoadingProgressEvent> binding;

    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        binding = new EventBinding<LoadingProgressEvent>(SetVisualize);
        EventBus<LoadingProgressEvent>.Register(binding);
        Silder.onValueChanged.RemoveAllListeners();
        Silder.onValueChanged.AddListener((value) =>
        {
            TextProgress.text = $"{value * 100:F0}%";
        });
        Reset();
    }

    public void SetVisualize(LoadingProgressEvent loadingProgressEvent)
    {
        Silder.DOValue(loadingProgressEvent.Progress,0.3f);
        TextDesc.text = loadingProgressEvent.Description;

    }
    
    public void Reset()
    {
        Silder.value = 0;
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
        EventBus<LoadingProgressEvent>.Deregister(binding);
        Reset();
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
