using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;


public partial class GachaResultPopup : UIView
{
    //UIControlData
    
    GameObject itemViewPrefab;

    PrefabPool itemPool;
    List<GachaResultItemView> itemViews= new List<GachaResultItemView>();
    
    // 标记 prefab 是否加载完成
    private bool isPrefabLoaded = false;
    
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
        LoadPrefabAsync();
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        Bind(data as GachaSessionViewModel);
    }

    async UniTask Bind(GachaSessionViewModel viewModel)
    {
        // 等待 prefab 加载完成
        while (!isPrefabLoaded)
        {
            await UniTask.Yield();
        }
        
        if (viewModel == null)
        {
            Debug.LogError("GachaResultPopup.Bind: viewModel is null");
            return;
        }

        if (viewModel.items == null)
        {
            Debug.LogError("GachaResultPopup.Bind: items is null");
            return;
        }
        
        ClearItems();
        //itemPool ??= PrefabPool.Create(itemViewPrefab, "GachaResultItem");
        
        //CreateItemViewsAsync(viewModel.items, this.GetCancellationTokenOnDestroy()).Forget();
        //UIHelper.CreateFullScreenClick(transform, ()=>viewModel.OnSessionFinished.OnNext(Unit.Default));
        foreach (var itemVM in viewModel.items)
        {
            var go = PrefabPool.Get("GachaResultItem").Get(itemContainer);
            var view = go.GetComponent<GachaResultItemView>();
            view.Bind(itemVM);
            itemViews.Add(view);
        }
        
        fullScreenButton.onClick.RemoveAllListeners();
        fullScreenButton.onClick.AddListener(() => viewModel.OnSessionFinished.OnNext(Unit.Default));
    }

    // 在 OnInit 或 OnOpen 时加载 prefab（异步一次）并放进 PrefabPool
    private async UniTask LoadPrefabAsync()
    {
        var prefab = await ResourceManager.Instance.InstantiateItemAsync("ui/gacha/result_item");
        if (prefab == null)
        {
            Debug.LogError("GachaResultPopup prefab 加载失败");
            return;
        }

        itemPool = PrefabPool.Create(prefab, "GachaResultItem");
        isPrefabLoaded = true;
    }
    
    async UniTask CreateItemViewsAsync(IReadOnlyList<GachaEntryViewModel> items,
        CancellationToken ct)
    {
      
        foreach (var itemVM in items)
        {
            ct.ThrowIfCancellationRequested();

            //todo:添加父物体节点
            var go = await ResourceManager.Instance.InstantiateItemAsync("ui/gacha/result_item",itemContainer);
            if (go == null)
            {
                Debug.LogError("Instantiate result_item failed");
                continue;
            }

            var view = go.GetComponent<GachaResultItemView>();
            if (view == null)
            {
                Debug.LogError("Prefab missing GachaResultItemView component");
                continue;
            }

            if (itemVM == null)
            {
                Debug.LogError("itemVM is null");
                continue;
            }

            view.Bind(itemVM);
            
            itemViews.Add(view);
        }
    }

    void ClearItems()
    {
        foreach (var item in itemViews)
        {
            itemPool.Recycle(item.gameObject);
        }
        itemViews.Clear();
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
    }

    public override void OnRelease()
    {
        base.OnRelease();
    }
}
