using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class WeaponDetailMiddleView : MonoBehaviour
{
    [Header("左侧选项卡")]
    [SerializeField]
    DetailTabItem[] tabItems;
    [Header("右侧内容区")]
    [SerializeField]
    GameObject infoPanel;
    [SerializeField]
    GameObject enhancePanel;
    [SerializeField]
    GameObject refinePanel;

    WeaponDetailMiddleViewModel vm;
    readonly List<GameObject> panels = new List<GameObject>();
    readonly CompositeDisposable disposable = new CompositeDisposable();
    
    public void Bind(WeaponDetailMiddleViewModel viewModel)
    {
        vm = viewModel;
        
        panels.Clear();
        panels.Add(infoPanel);
        panels.Add(enhancePanel);
        panels.Add(refinePanel);
        
        disposable.Clear();
        BindTabItems();

        vm.currentTabIndex.Subscribe(OnTabChanged).AddTo(disposable);
        vm.currentWeaponVM.Value.needBreak
            .Subscribe(_ => RefreshEnhanceTabLabel())
            .AddTo(disposable);
    }

    void BindTabItems()
    {
        if (tabItems == null)
        {
            return;
        }

        string[] labels = { "详情", GetEnhanceTabLabel(), "精炼" };
        for (int i = 0; i < tabItems.Length; i++)
        {
            if (tabItems[i] == null)
            {
                continue;
            }
            
            int index = i;
            tabItems[i].Bind(index, labels[i], () => vm.SelectTab(index));
        }
    }

    public void RefreshEnhanceTabLabel()
    {
        if (tabItems == null || tabItems.Length <= 1)
        {
            return;
        }

        tabItems[1].SetLabel(GetEnhanceTabLabel());
    }

    string GetEnhanceTabLabel()
    {
        var weapon = vm.currentWeaponVM.Value;
        if (weapon == null)
        {
            return "强化";
        }

        return weapon.needBreak.Value ? "突破" : "强化";
    }

    void OnTabChanged(int index)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            panels[i].SetActive(i==index);
        }

        if (tabItems == null)
        {
            return;
        }

        for (int i = 0; i < tabItems.Length; i++)
        {
            tabItems[i].SetSelected(i == index);
        }
    }

    void OnDestroy()
    {
        disposable.Dispose();
    }
}
