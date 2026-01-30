using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaResultDetailViewModel
{
    readonly GachaSessionViewModel sessionVM;
    CompositeDisposable disposable = new CompositeDisposable();
    
    public IReadOnlyReactiveProperty<GachaEntryViewModel> CurrentItem { get; }

    public IReadOnlyReactiveProperty<bool> HasNext => sessionVM.HasNext;
    
    public ReactiveCommand NextCommand { get;} = new ReactiveCommand();
    public ReactiveCommand SkipCommand { get;} = new ReactiveCommand();
    public GachaResultDetailViewModel(GachaSessionViewModel viewModel)
    {
        sessionVM = viewModel;
        CurrentItem = viewModel.CurrentItem;
        
        NextCommand
            .Subscribe(_ =>
            {
                if (sessionVM.HasNext.Value)
                {
                    sessionVM.Next();
                }
                else
                {
                    OpenResultPopup();
                }
                Debug.Log("执行 NextCommand，当前索引：" + sessionVM.currentIndex.Value);
            })
            .AddTo(disposable);
        SkipCommand
            .Subscribe(_ =>
            {
                sessionVM.Skip();
                OpenResultPopup();
                Debug.Log("执行 SkipCommand，当前索引：" + sessionVM.currentIndex.Value);
            })
            .AddTo(disposable);
    }

    void OpenResultPopup()
    {
        /*UIManager.Instance.Open(UIType.GachaResultPopup,sessionVM);
        sessionVM.OnSessionFinished.Subscribe(_ =>
        {
            UIManager.Instance.Close(UIType.GachaResultPopup); 
        });*/
        sessionVM.OnPreviewFinished.OnNext(Unit.Default);
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}

