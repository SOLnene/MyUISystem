using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class SingleResultViewModel
{
    public ReactiveProperty<GachaEquipItemViewModel> item = new ReactiveProperty<GachaEquipItemViewModel>();

    CompositeDisposable disposable = new CompositeDisposable();
    
    GachaViewModel vm;
    public SingleResultViewModel(GachaViewModel viewModel)
    {
        vm = viewModel;
        viewModel.currentIndex.Subscribe(
            index =>
            {
                if (index < 0 || index > vm.lastDrawnItems.Count)
                {
                    return;
                }
                item.Value = viewModel.lastDrawnItems[index];
            }).AddTo(disposable);
    }

  
    
    public void SetItem(GachaEquipItemViewModel equipItem)
    {
        item.Value = equipItem;
    }

    public void ShowNext()
    {
        vm.ShowNext();
    }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}
 