using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class GachaResultViewModel : IDisposable
{
    public IReadOnlyList<GachaEntryViewModel> Items { get; }

    public readonly ReactiveCommand<GachaEntryViewModel> OnEntryClicked = new ReactiveCommand<GachaEntryViewModel>();

    public readonly Subject<Unit> OnConfirm = new Subject<Unit>();
    CompositeDisposable disposable = new CompositeDisposable();
    public GachaResultViewModel(IReadOnlyList<GachaEntryViewModel> items)
    {
        Items = items;
    }

    public void Dispose()
    {
        disposable.Dispose();
        OnConfirm.Dispose();
    }
}
