using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaFlowController : IDisposable
{

    GachaViewModel Vm;
    readonly CompositeDisposable disposable = new CompositeDisposable();

    public GachaFlowController( )
    {
        
    }

    public void StartGachaFlow()
    {
        UIManager.Instance.OpenWithView(UIType.GachaView,callback: view =>
        {
            var gachaView = view as GachaView;
            Bind(gachaView.vm);
        });
    }
    
    public void Bind(GachaViewModel viewModel)
    {
        Vm = viewModel;
        Vm.OnSessionStarted
            .Subscribe(StartGachaSession)
            .AddTo(disposable);
    }
    void StartGachaSession(GachaSessionViewModel session)
    {
        UIManager.Instance.Open(
            UIType.GachaResultDetailPopup,
            session
            );

        session.OnPreviewFinished
            .Subscribe(_ => ShowResult(session))
            .AddTo(disposable);

        session.OnSessionFinished
            .Subscribe(_ =>
            {
                UIManager.Instance.Close(UIType.GachaResultPopup);
                Debug.Log("抽卡会话结束");
            })
            .AddTo(disposable);
    }
    
    void ShowResult(GachaSessionViewModel session)
    {
        UIManager.Instance.Close(UIType.GachaResultDetailPopup);

        var resultVm = new GachaResultViewModel(session.Items);

        resultVm.OnConfirm
            .Subscribe(_ =>
            {
                session.OnSessionFinished.OnNext(Unit.Default);
            })
            .AddTo(disposable);

        resultVm.OnEntryClicked
            .Subscribe(OpenEntryDetail)
            .AddTo(disposable);

        UIManager.Instance.Open(UIType.GachaResultPopup, resultVm);
    }
    
    public void OpenEntryDetail(GachaEntryViewModel viewModel)
    {
        if (viewModel.EntryType == GachaEntryType.Character)
        {
            Debug.Log("Clicked character: " + viewModel.Name);
        }
        if (viewModel.EntryType == GachaEntryType.Equip)
        {
            Debug.Log("Clicked equip: " + viewModel.Name);
            var equipVm = ConvertToEquip(viewModel);
            UIManager.Instance.Open(UIType.EquipDetailView, equipVm);
            UIManager.Instance.Close(UIType.GachaView);
            UIManager.Instance.Close(UIType.GachaResultPopup);
        }
    }
    
    EquipItemViewModel ConvertToEquip(GachaEntryViewModel entry)
    {
        return new EquipItemViewModel(new EquipItem(GameDatabase.ItemDatabase.GetItemByKey(entry.Name) as EquipDefinition));
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
