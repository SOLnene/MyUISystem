using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;
public class GameContext: Singleton<GameContext>
{
    public BackpackViewModel BackpackVM { get; private set; }

    public InventoryRepository InventoryRepository { get; private set; }
    //全项目只有一个实现
    public GachaService GachaService { get; private set; }
    //可能有多个不同的实现
    public IGachaVisualProvider GachaVisualProvider { get; private set; }
    public async UniTask Init()
    {
        await GameDatabase.Init();
        //backpackVM = new BackpackViewModel();
        var model = new BackpackModel();
        //todo:改为使用 Installer + DI 容器注入
        BackpackVM = new BackpackViewModel(model);
        InventoryRepository = new InventoryRepository(model, BackpackVM);

        LocalGachaSchedule gachaSchedule = new LocalGachaSchedule();
        GachaPoolProvider poolProvider = new GachaPoolProvider(GameDatabase.GachaPoolDatabase, gachaSchedule);
        GachaService = new GachaService(poolProvider);
        GachaVisualProvider = new GachaVisualProvider(GameDatabase.CharaVisualDatabase);
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