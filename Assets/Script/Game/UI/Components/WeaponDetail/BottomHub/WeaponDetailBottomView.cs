using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    Button refineBtn;
    [SerializeField]
    TextMeshProUGUI enhanceGoldText;
    [SerializeField]
    TextMeshProUGUI promoteGoldText;
    [SerializeField]
    TextMeshProUGUI refineGoldText;
    
    [SerializeField]
    GameObject infoContent;
    [SerializeField]
    GameObject enhanceContent;
     [SerializeField]
    GameObject promoteContent;
    [SerializeField]
    GameObject refineContent;
    [SerializeField]
    AnimatedPanel animatedRoot;
    [SerializeField]
    BottomContentStateView enhanceContentState;

    WeaponDetailBottomViewModel vm;
    
    CompositeDisposable disposable = new();
    public void Bind(WeaponDetailBottomViewModel viewModel)
    {
        vm = viewModel;
        disposable.Clear();

        vm.totalCostGold
            .Subscribe(value => {
                SetCostGold(value);
            })
            .AddTo(disposable);

        // 按钮事件绑定（ReactiveCommand 绑定）
        if (storyBtn)
            storyBtn.onClick.AsObservable().Subscribe(_ => vm.onStoryClick.Execute()).AddTo(disposable);
        if (quickEquipBtn)
            quickEquipBtn.onClick.AsObservable().Subscribe(_=>vm.onQuickEquipClick.Execute()).AddTo(disposable);
        if (enhanceBtn) 
            enhanceBtn.onClick.AsObservable().Subscribe(_=>vm.onEnhanceClick.Execute()).AddTo(disposable);
        if (breakBtn)
            breakBtn.onClick.AsObservable().Subscribe(_ => vm.onBreakoutClick.Execute()).AddTo(disposable);
        if (refineBtn == null && refineContent != null)
            refineBtn = refineContent.GetComponentInChildren<Button>(true);
        if (refineBtn)
            refineBtn.onClick.AsObservable().Subscribe(_ => vm.onRefineClick.Execute()).AddTo(disposable);
    }

    void SetCostGold(int value)
    {
        if (enhanceGoldText)
            enhanceGoldText.text = $"{value}";

        if (promoteGoldText)
            promoteGoldText.text = $"{value}";
        if (refineGoldText)
            refineGoldText.text = $"{value}";
    }

    public void Refresh()
    {
        ApplyContentVisible();
    }

    void ApplyContentVisible()
    {
        if (vm == null)
            return;

        int selectedTabIndex = vm.selectedTabIndex.Value;
        bool canBreakout = vm.canBreakout.Value;

        bool isInfo = selectedTabIndex == (int)WeaponDetailTab.Info;
        bool isEnhance = selectedTabIndex == (int)WeaponDetailTab.Enhance;
        bool isRefine = selectedTabIndex == (int)WeaponDetailTab.Refine;

        infoContent.SetActive(isInfo);
        enhanceContent.SetActive(isEnhance && !canBreakout);
        if (isEnhance && !canBreakout)
            enhanceContentState.SetState(BottomContentStateView.State.Normal);
        promoteContent.SetActive(isEnhance && canBreakout);
        refineContent.SetActive(isRefine);
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

    public void ShowEnhanceBottomNormal()
    {
        enhanceContentState.SetState(BottomContentStateView.State.Normal);
    }

    public void ShowEnhanceBottomProcessing()
    {
        enhanceContentState.SetState(BottomContentStateView.State.Processing);
    }

    public void ShowEnhanceBottomResult()
    {
        enhanceContentState.SetState(BottomContentStateView.State.Result);
    }

    
    private void OnDestroy()
    {
        disposable.Dispose();
    }
}
