using System.Collections;
using System.Collections.Generic;
using Game.UI.Components.CharacterDetail;
using UnityEngine;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine.UI;
public class EnhancePanelView : MonoBehaviour
{
    [SerializeField]
    GameObject upgradePanel;
    [SerializeField]
    GameObject breakOutPanel;
    
    [Header("可升级情况界面")]
    [SerializeField]
    GameObject unMaxLevelPanel;
    [SerializeField]
    TextMeshProUGUI levelValueText;
    [SerializeField]
    List<GameObject> arrows;

    [SerializeField]
    TextMeshProUGUI expValueText;
    [SerializeField]
    TextMeshProUGUI expPlusValueText;
    [SerializeField]
    EnhancePanelExpBar expBar;
    
    [Header("突破界面")]
    [SerializeField]
    TextMeshProUGUI currentLevelText;
    [SerializeField]
    TextMeshProUGUI afterLevelText;
    [SerializeField]
    GameObject currentStarContent;
    [SerializeField]
    GameObject afterStarContent;
    
    [Header("通用")]
    [SerializeField]
    StatItemView[] statItemViews;
    
    [Space]
    [Header("右下面板")]
    [SerializeField]
    EnhanceRightBottomView rightBottomView;
    
    EnhancePanelViewModel vm;

    CompositeDisposable rootDisposable = new();
    CompositeDisposable uiDisposable=new();
    
    public void Bind(EnhancePanelViewModel viewModel)
    {
        vm = viewModel;
        
        expBar.BindData();
        BindStatItems();
        
        viewModel.weaponVM
            .Where(w => w != null)
            .Subscribe(weapon =>
            {
                weapon.needBreak.Subscribe(b =>
                {
                    upgradePanel.SetActive(!b);
                    breakOutPanel.SetActive(b);
                    
                    uiDisposable.Clear();
                    
                    if (b)
                    {
                        BindBreakoutUI(weapon);
                    }
                    else
                    {
                        BindUpgradeUI(weapon);
                    }
                }).AddTo(rootDisposable);

            })
            .AddTo(rootDisposable);
        
        
        rightBottomView.Bind(vm.rightBottomVM);
    }

    void BindStatItems()
    {
        statItemViews[0].Bind(vm.statItemVMs[0]);
        statItemViews[1].Bind(vm.statItemVMs[1]);
    }

    void BindUpgradeUI(EquipItemViewModel weapon)
    {
        weapon.level.Subscribe(value =>
        {
            if (levelValueText)
                levelValueText.text = $"Lv.{value}";
        }).AddTo(uiDisposable);
    
        Observable.CombineLatest(weapon.currentExp, weapon.nextLevelExp, 
                (cur, next) => new { cur, next })
            .Subscribe(exp =>
            {
                if (expValueText)
                    expValueText.text = $"{exp.cur}/{exp.next}";
            }).AddTo(uiDisposable);
    
        //经验条绑定
        Observable
            .CombineLatest(
                weapon.currentExp.StartWith(weapon.currentExp.Value),
                weapon.nextLevelExp.StartWith(weapon.nextLevelExp.Value),
                vm.previewExp.StartWith(vm.previewExp.Value),
                (cur, next,previewExp) => new { cur, next,previewExp })
            .Subscribe(exp =>
            {
                if (expBar)
                    expBar.SetValue(exp.cur, exp.next,exp.cur+exp.previewExp);
            })
            .AddTo(uiDisposable);
        
        vm.rightBottomVM.totalExp.Subscribe(exp =>
        {
            expPlusValueText.text = $"+{exp}";
        }).AddTo(uiDisposable);
    }
    
    void BindBreakoutUI(EquipItemViewModel weapon)
    {
        SetStars(weapon);
        currentLevelText.text = $"Lv.{weapon.level.Value}";
        afterLevelText.text = $"Lv.{weapon.nextRankMaxLevel.Value}";
    }
    
    void SetStars(EquipItemViewModel viewModel)
    {
        var currentIcons = currentStarContent.GetComponentsInChildren<Image>();
        for (int i = 0; i < currentIcons.Length; i++)
        {
            currentIcons[i].color = i < viewModel.rank.Value ? Color.white : Color.grey;
        }
        afterLevelText.gameObject.SetActive(!viewModel.maxRanked.Value);
        afterStarContent.SetActive(!viewModel.maxRanked.Value);
        if (viewModel.maxRanked.Value)
        {
            return;
        }
        var afterIcons = afterStarContent.GetComponentsInChildren<Image>();
        for (int i = 0; i < afterIcons.Length; i++)
        {
            afterIcons[i].color = i < viewModel.rank.Value + 1 ? Color.white : Color.grey;
        }
    }
    
    void OnDestroy()
    {
        uiDisposable.Dispose();
    }
    
}
