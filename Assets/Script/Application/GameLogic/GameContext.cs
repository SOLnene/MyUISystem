using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
public class GameContext: Singleton<GameContext>
{
    public BackpackViewModel BackpackVM { get; private set; }

    public InventoryRepository InventoryRepository { get; private set; }
    public override void Init()
    {
        base.Init();
        //backpackVM = new BackpackViewModel();
        var model = new BackpackModel();
        BackpackVM = new BackpackViewModel(model);
        InventoryRepository = new InventoryRepository(model, BackpackVM);
    }
}

public class InventoryRepository
{
    public  readonly BackpackModel model;
    private readonly BackpackViewModel vm;

    public IEnumerable<InventoryItem> GetAllItems() => model.Items;
    public IEnumerable<ItemSlotViewModel> GetAllSlotVMs() => vm.SlotsViewModels;

  
    
    public InventoryRepository(BackpackModel model, BackpackViewModel vm)
    {
        this.model = model;
        this.vm = vm;
    }

    /*public IObservable<IReadOnlyList<InventoryItem>> ObserveItems()
    {
        return model.Items.().Select(_ => model.Items).StartWith(model.Items);
    }*/
}