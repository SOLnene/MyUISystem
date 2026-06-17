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
    public CharacterRepository CharacterRepository { get; private set; }
    //全项目只有一个实现
    public GachaService GachaService { get; private set; }
    //可能有多个不同的实现
    public IGachaVisualProvider GachaVisualProvider { get; private set; }
    public async UniTask Init()
    {
        await GameDatabase.Init();
        //backpackVM = new BackpackViewModel();
        //todo:改为使用 Installer + DI 容器注入
        InventoryRepository = new InventoryRepository();
        BackpackVM = new BackpackViewModel(InventoryRepository);
        CharacterRepository = new CharacterRepository();

        LocalGachaSchedule gachaSchedule = new LocalGachaSchedule();
        GachaPoolProvider poolProvider = new GachaPoolProvider(GameDatabase.GachaPoolDatabase, gachaSchedule);
        GachaService = new GachaService(poolProvider);
        GachaVisualProvider = new GachaVisualProvider(GameDatabase.CharaVisualDatabase);
    }
}

public class InventoryRepository
{
    private readonly List<InventoryItem> items = new List<InventoryItem>();

    public IReadOnlyList<InventoryItem> GetAllItems() => items;

    public void AddItem(InventoryItem inventoryItem) => items.Add(inventoryItem);

    public void RemoveItem(InventoryItem inventoryItem) => items.Remove(inventoryItem);

    /*public IObservable<IReadOnlyList<InventoryItem>> ObserveItems()
    {
        return model.Items.().Select(_ => model.Items).StartWith(model.Items);
    }*/
}
