using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    EnhancePanelView enhancePanelView;
    [SerializeField]
    GameObject refinePanel;
    [SerializeField]
    RefinePanelView refinePanelView;
    [SerializeField]
    AnimatedPanel animatedRoot;

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

        if (enhancePanelView == null && enhancePanel != null)
            enhancePanelView = enhancePanel.GetComponent<EnhancePanelView>();
        if (refinePanelView == null && refinePanel != null)
            refinePanelView = refinePanel.GetComponent<RefinePanelView>();
        
        disposable.Clear();
        BindTabItems();

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

    public void ApplyTabImmediate(int index)
    {
        Refresh();
        ShowImmediate();
    }

    public void Refresh()
    {
        if (vm == null)
            return;

        int index = vm.currentTabIndex.Value;
        SetPanelActive(index);
        SetTabSelected(index);

        if (index == (int)WeaponDetailTab.Enhance && enhancePanelView != null)
            enhancePanelView.Refresh();
        if (index == (int)WeaponDetailTab.Refine && refinePanelView != null)
            refinePanelView.Refresh();
    }

    public void SetPanelActive(int index)
    {
        for (int i = 0; i < panels.Count; i++)
            SetPanelActive(i, i == index);
    }

    public void SetPanelActive(int index, bool active)
    {
        if (index < 0 || index >= panels.Count || panels[index] == null)
            return;

        panels[index].SetActive(active);
    }

    public async UniTask HideContent()
    {
        if (animatedRoot != null)
            await animatedRoot.Hide();
    }

    public async UniTask ShowContent()
    {
        if (animatedRoot != null)
            await animatedRoot.Show();
    }

    public void ShowImmediate()
    {
        animatedRoot?.Show(true).Forget();
    }

    public void SetTabSelected(int index)
    {
        if (tabItems == null)
            return;

        for (int i = 0; i < tabItems.Length; i++)
            tabItems[i].SetSelected(i == index);
    }

    void OnDestroy()
    {
        disposable.Dispose();
    }
}
