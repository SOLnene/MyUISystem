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
                sessionVM.Next();
                Debug.Log("执行 NextCommand，当前索引：" + sessionVM.CurrentIndex.Value);
            })
            .AddTo(disposable);
        SkipCommand
            .Subscribe(_ =>
            {
                sessionVM.SkipReveal();
                Debug.Log("执行 SkipCommand，当前索引：" + sessionVM.CurrentIndex.Value);
            })
            .AddTo(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}

