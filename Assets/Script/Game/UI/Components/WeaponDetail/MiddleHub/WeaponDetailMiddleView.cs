using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class WeaponDetailMiddleView : MonoBehaviour
{
    [Header("左侧选项卡")]
    [SerializeField]
    UITabSelector tabSelector;
    [Header("右侧内容区")]
    [SerializeField]
    GameObject infoPanel;
    [SerializeField]
    GameObject enhancePanel;
    [SerializeField]
    GameObject refinePanel;

    WeaponDetailMiddleViewModel vm;
    readonly List<GameObject> panels = new List<GameObject>();
    
    public void Bind(WeaponDetailMiddleViewModel viewModel)
    {
        vm = viewModel;
        
        panels.Clear();
        panels.Add(infoPanel);
        panels.Add(enhancePanel);
        panels.Add(refinePanel);

        tabSelector.Bind(vm.currentTabIndex);

        vm.currentTabIndex.Subscribe(OnTabChanged).AddTo(this);
    }

    void OnTabChanged(int index)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            panels[i].SetActive(i==index);
        }
    }
}
