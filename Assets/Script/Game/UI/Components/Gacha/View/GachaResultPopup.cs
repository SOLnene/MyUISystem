using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
		[ControlBinding]
		private Button fullScreenButton;
		[ControlBinding]
		private GachaResultListView itemContainer;
		[ControlBinding]
		private GachaResultItemView[] resultItem;

		#pragma warning restore 0649
#endregion


    GameObject itemViewPrefab;
    
    PrefabPool itemPool;
    List<GachaResultItemView> itemViews;
    public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
    {
        base.OnInit(uiControlData,handle);
        itemViews = new List<GachaResultItemView>(resultItem);
    }

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        Bind(data as GachaResultViewModel);
    }

    async UniTask Bind(GachaResultViewModel viewModel)
    {
        
        if (viewModel == null)
        {
            Debug.LogError("GachaResultPopup.Bind: viewModel is null");
            return;
        }

        if (viewModel.Items == null)
        {
            Debug.LogError("GachaResultPopup.Bind: items is null");
            return;
        }
        itemContainer.ResetToIdle(itemViews);
        itemContainer.SetClick(viewModel.OnEntryClicked);
        
        for(int i=0;i<viewModel.Items.Count;i++)
        {
            resultItem[i].Bind(viewModel.Items[i],viewModel.OnEntryClicked);
        }
       
        fullScreenButton.onClick.RemoveAllListeners();
        fullScreenButton.onClick.AddListener(() =>
        {
            if (itemContainer.IsFinished())
            {
                viewModel.OnConfirm.OnNext(Unit.Default);
            }
            else
            {
                itemContainer.SkipToEnd(itemViews);
            }
        });
        itemContainer.PlayEnter(itemViews).Forget();
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
