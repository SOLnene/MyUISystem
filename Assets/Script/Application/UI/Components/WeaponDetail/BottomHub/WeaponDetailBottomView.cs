using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class WeaponDetailBottomView : MonoBehaviour
{
    //详情界面
    [SerializeField]
    Button storyBtn;
    [SerializeField]
    Button quickEquipBtn;
    [SerializeField]
    Button enhanceBtn;
    [SerializeField]
    Button breakBtn;
    [SerializeField]
    TextMeshProUGUI goldText;
    [SerializeField]
    GameObject infoContent;
    [SerializeField]
    GameObject enhanceContent;
    [SerializeField]
    GameObject refineContent;

    WeaponDetailBottomViewModel vm;
    
    CompositeDisposable disposable = new();
    public void Bind(WeaponDetailBottomViewModel viewModel)
    {
        vm = viewModel;
        disposable.Clear();

        vm.totalCostGold
            .Subscribe(value => {
                if (goldText) goldText.text = $"{value}";
            })
            .AddTo(disposable);

        vm.canBreakout.Subscribe(b =>
        {
            enhanceBtn.gameObject.SetActive(!b);
            breakBtn.gameObject.SetActive(b);
        }).AddTo(disposable);
        
        // 按钮事件绑定（ReactiveCommand 绑定）
        if (storyBtn)
            storyBtn.onClick.AsObservable().Subscribe(_ => vm.onStoryClick.Execute()).AddTo(disposable);
        if (quickEquipBtn)
            quickEquipBtn.onClick.AsObservable().Subscribe(_=>vm.onQuickEquipClick.Execute()).AddTo(disposable);
        if (enhanceBtn) 
            enhanceBtn.onClick.AsObservable().Subscribe(_=>vm.onEnhanceClick.Execute()).AddTo(disposable);
        if (breakBtn)
            breakBtn.onClick.AsObservable().Subscribe(_ => vm.onBreakoutClick.Execute()).AddTo(disposable);
        vm.selectedTabIndex.Subscribe(index => SetActive(index)).AddTo(disposable);
    }

    void SetActive(int index)
    {
        infoContent.SetActive(index==0);
        enhanceContent.SetActive(index==1);
        refineContent.SetActive(index==2);
    }
    
    private void OnDestroy()
    {
        disposable.Dispose();
    }
}
