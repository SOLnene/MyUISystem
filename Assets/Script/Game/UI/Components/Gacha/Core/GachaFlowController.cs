using System;
using System.Collections;
using System.Collections.Generic;
using Game.Domain.Character;
using Game.UI.Components.CharacterDetail;
using UniRx;
using UnityEngine;

public class GachaFlowController : IDisposable
{
    readonly CompositeDisposable sessionDisposables = new CompositeDisposable();
    GachaSessionViewModel currentSession;
    GachaResultViewModel resultViewModel;

    public void StartSession(GachaSessionViewModel session)
    {
        ClearSession();
        currentSession = session;

        session.OnPreviewFinished
            .Take(1)
            .Subscribe(_ => HandleRevealFinished(session))
            .AddTo(sessionDisposables);

        session.OnSessionFinished
            .Take(1)
            .Subscribe(_ =>
            {
                UIManager.Instance.Close(UIType.GachaResultPopup);
                Debug.Log("抽卡会话结束");
                ClearSession();
            })
            .AddTo(sessionDisposables);

        UIManager.Instance.Open(
            UIType.GachaResultDetailPopup,
            session
            );
    }
    
    void HandleRevealFinished(GachaSessionViewModel session)
    {
        UIManager.Instance.Close(UIType.GachaResultDetailPopup);

        if (session.Items.Count == 1)
        {
            session.FinishSession();
            return;
        }

        resultViewModel = new GachaResultViewModel(session.Items);

        resultViewModel.OnConfirm
            .Take(1)
            .Subscribe(_ =>
            {
                session.FinishSession();
            })
            .AddTo(sessionDisposables);

        resultViewModel.OnEntryClicked
            .Subscribe(OpenEntryDetail)
            .AddTo(sessionDisposables);

        UIManager.Instance.Open(UIType.GachaResultPopup, resultViewModel);
    }
    
    public void OpenEntryDetail(GachaEntryViewModel viewModel)
    {
        if (viewModel.EntryType == GachaEntryType.Character)
        {
            Debug.Log("Clicked character: " + viewModel.Name);
            var characterVm = ConvertToCharacter(viewModel);
            UIManager.Instance.Open(UIType.CharacterDetailView, characterVm);
            UIManager.Instance.Close(UIType.GachaView);
            UIManager.Instance.Close(UIType.GachaResultPopup);
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
        return new EquipItemViewModel(new EquipItem(GameDatabase.ItemDatabase.GetItemByKey(entry.EntryKey) as EquipDefinition));
    }

    CharacterDetailViewModel ConvertToCharacter(GachaEntryViewModel entry)
    {
        CharacterDefinition definition = GameDatabase.CharacterDatabase.Get(entry.EntryKey);
        CharacterModel model = CharacterFactory.Create(definition, 1);
        return new CharacterDetailViewModel(model);
    }

    public void CancelSession()
    {
        UIManager.Instance.Close(UIType.GachaResultDetailPopup);
        UIManager.Instance.Close(UIType.GachaResultPopup);
        if (currentSession != null && currentSession.Phase.Value != GachaSessionPhase.Finished)
        {
            currentSession.FinishSession();
            return;
        }

        ClearSession();
    }

    void ClearSession()
    {
        sessionDisposables.Clear();
        currentSession = null;
        resultViewModel?.Dispose();
        resultViewModel = null;
    }
    
    public void Dispose()
    {
        ClearSession();
        sessionDisposables.Dispose();
    }
}
