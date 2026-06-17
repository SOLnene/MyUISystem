using System;
using Cysharp.Threading.Tasks;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
namespace Game.UI.Components.CharacterDetail
{
    public partial class CharacterDetailView : UIView
    {
        //UIControlData
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
        [ControlBinding]
        private RectTransform topBarLayer;
        [ControlBinding]
        private RectTransform contentLayer;
        [ControlBinding]
        private TextMeshProUGUI charaText;
        [ControlBinding]
        private CharacterDetailContentView contentView;
        [ControlBinding]
        private Image final;
        
		#pragma warning restore 0649
#endregion

        [SerializeField]
        DetailTabItem[] tabItems;
        [SerializeField]
        AnimatedPanel topPanel;
        [SerializeField]
        AnimatedPanel tabPanel;
        [SerializeField]
        AnimatedPanel infoPanel;
        [SerializeField]
        CharacterEnhancePanel enhancePanelView;
        [SerializeField]
        CharacterPromoteView promotePanelView;
        [SerializeField]
        CharacterDetailTopView topView;
    
        
        private const float TOP_BAR_HEIGHT = 150f;   
        private const float BOTTOM_BAR_HEIGHT = 140f;


        //tab标签
        static readonly string[] Labels =
        {
            "属性",
            "装备",
            "圣遗物",
            "详情"
        };
          
        CharacterDetailViewModel vm;

        int currentIndex = -1;
        bool isSwitchingTab;
        bool isPlayingResultFlow;
        CompositeDisposable disposable = new CompositeDisposable();
        public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
        {
            base.OnInit(uiControlData,handle);
        }

        public override void OnOpen(object data)
        {
            base.OnOpen(data);
            vm = data as CharacterDetailViewModel;
            Bind(vm);
        }

        public void Bind(CharacterDetailViewModel viewModel)
        {
            disposable.Clear();
            topView.Bind(viewModel.model.Name.Value, OnCancel);
            
            final.gameObject.SetActive(false);
            contentView.Bind(viewModel.contentViewModel);
           
            enhancePanelView.Bind(viewModel.enhanceViewmodel);
            promotePanelView.Bind(viewModel.promoteViewmodel);
            BackToDetailMainViewImmediate();
            // 初始化时先决定显示哪个面板
            //RefreshUpgradeOrPromotePanel();

            // 升级后/突破后重新判断一次
            viewModel.enhanceViewmodel.requestPlayEnhanceResult
                .Subscribe(result => PlayEnhanceResultFlow(result).Forget())
                .AddTo(disposable);

            viewModel.onBackToMain
                .Subscribe(_ => BackToDetailMainView())
                .AddTo(disposable);

            viewModel.promoteViewmodel.requestPlayPromoteResult
                .Subscribe(result => PlayPromoteResultFlow(result).Forget())
                .AddTo(disposable);

            contentView.InfoPanelView.onUpgradeClick += OpenUpgradeOrPromotePanel;

            SetTabItems();
            //初始化为idle
            //todo:切换角色初始化
            //todo:不写死
            SwitchTab(0, true);
            ShowMainPanels(false).Forget();
            //todo:如果脸部动画很明显，需要给facepreset也做一个immediate方法
        }
    
        void RefreshUpgradeOrPromotePanel()
        {
            RefreshUpgradeOrPromotePanelAsync(false).Forget();
        }

        async UniTask RefreshUpgradeOrPromotePanelAsync(bool instant)
        {
            // 规则：小于当前 rank 最大等级 -> 升级；达到/超过 -> 突破
            int level = vm.model.LevelRP.Value;
            int maxLevel = vm.model.GetCurrentMaxLevel();
            bool showUpgrade = level < maxLevel;

            if (showUpgrade)
            {
                promotePanelView.gameObject.SetActive(false);
                await enhancePanelView.ShowPanel(instant);
            }
            else
            {
                enhancePanelView.gameObject.SetActive(false);
                await promotePanelView.ShowPanel(instant);
            }
        }

        void OpenUpgradeOrPromotePanel()
        {
            OpenUpgradeOrPromotePanelAsync().Forget();
        }

        async UniTask OpenUpgradeOrPromotePanelAsync()
        {
            await HideMainPanels(false);
            await RefreshUpgradeOrPromotePanelAsync(false);
        }

        void BackToDetailMainView()
        {
            BackToDetailMainViewAsync(false).Forget();
            Debug.Log("返回角色详情主界面");
        }

        void BackToDetailMainViewImmediate()
        {
            contentView.gameObject.SetActive(true);
            topBarLayer.gameObject.SetActive(true);
            enhancePanelView.gameObject.SetActive(false);
            promotePanelView.gameObject.SetActive(false);
            topPanel.Show(true).Forget();
            tabPanel.Show(true).Forget();
            infoPanel.Show(true).Forget();
        }

        async UniTask BackToDetailMainViewAsync(bool instant)
        {
            await HideUpgradeOrPromotePanel(instant);
            await ShowMainPanels(instant);
        }

        async UniTask ShowMainPanels(bool instant)
        {
            contentView.gameObject.SetActive(true);
            topBarLayer.gameObject.SetActive(true);
            await UniTask.WhenAll(
                topPanel.Show(instant),
                tabPanel.Show(instant),
                infoPanel.Show(instant)
            );
        }

        async UniTask HideMainPanels(bool instant)
        {
            await UniTask.WhenAll(
                topPanel.Hide(instant),
                tabPanel.Hide(instant),
                infoPanel.Hide(instant)
            );
            contentView.gameObject.SetActive(false);
            topBarLayer.gameObject.SetActive(false);
        }

        async UniTask HideUpgradeOrPromotePanel(bool instant)
        {
            if (enhancePanelView.gameObject.activeSelf)
            {
                await enhancePanelView.HidePanel(instant);
            }

            if (promotePanelView.gameObject.activeSelf)
            {
                await promotePanelView.HidePanel(instant);
            }
        }

        async UniTask PlayEnhanceResultFlow(EnhanceResultData result)
        {
            if (isSwitchingTab || isPlayingResultFlow)
                return;

            isPlayingResultFlow = true;
            try
            {
                enhancePanelView.ShowEnhanceProcessing();
                await enhancePanelView.PlayEnhanceExpProgress(result);

                if (result.oldLevel == result.newLevel)
                {
                    if (result.needSwitchContent)
                    {
                        enhancePanelView.ShowEnhanceMaxLevelText("已达到当前等级上限");
                        await RefreshUpgradeOrPromotePanelAsync(false);
                    }
                    else
                    {
                        enhancePanelView.ShowEnhanceNormal(true);
                    }

                    return;
                }

                Action onNewLevelShown = result.needSwitchContent
                    ? () => enhancePanelView.ShowEnhanceMaxLevelText("已达到当前等级上限")
                    : () => enhancePanelView.ShowEnhanceNormal(true);

                await enhancePanelView.PlayEnhanceLevelResult(result, onNewLevelShown);

                if (result.needSwitchContent)
                    await RefreshUpgradeOrPromotePanelAsync(false);
            }
            finally
            {
                isPlayingResultFlow = false;
            }
        }

        async UniTask PlayPromoteResultFlow(PromoteLevelResultData result)
        {
            if (isSwitchingTab || isPlayingResultFlow)
                return;

            isPlayingResultFlow = true;
            try
            {
                promotePanelView.ShowPromoteProcessing();

                Action onNewStateShown = () => promotePanelView.ShowPromoteResultText("突破成功");

                await promotePanelView.PlayPromoteResult(result, onNewStateShown);
                await RefreshUpgradeOrPromotePanelAsync(false);
            }
            finally
            {
                isPlayingResultFlow = false;
            }
        }

        void SetTabItems()
        {
            if (tabItems == null)
            {
                return;
            }

            for (int i = 0; i < tabItems.Length; i++)
            {
                int index = i;
                tabItems[i].Bind(index, Labels[i],() => OnTabClicked(index));
            }
        }

        void OnTabClicked(int index)
        {
            if (!CanSwitchTab(index))
            {
                return;
            }

            SwitchTabAsync(index, false).Forget();
        }

        bool CanSwitchTab(int index)
        {
            if (tabItems == null || index < 0 || index >= tabItems.Length)
            {
                return false;
            }

            if (currentIndex == index)
            {
                return false;
            }

            if (isSwitchingTab)
            {
                return false;
            }

            return ModelViewer.Instance == null || !ModelViewer.Instance.IsInTransition;
        }

        void SwitchTab(int index, bool instant)
        {
            SwitchTabAsync(index, instant).Forget();
        }

        async UniTask SwitchTabAsync(int index, bool instant)
        {
            if (currentIndex >= 0 && currentIndex < tabItems.Length)
            {
                tabItems[currentIndex].SetSelected(false, instant);
            }

            currentIndex = index;
            tabItems[currentIndex].SetSelected(true, instant);

            isSwitchingTab = true;
            try
            {
                await ApplyCurrentTab(instant);
            }
            finally
            {
                isSwitchingTab = false;
            }
        }

        async UniTask ApplyCurrentTab(bool instant)
        {
            if (ModelViewer.Instance != null)
            {
                ModelViewer.Instance.SwitchPreview(currentIndex, instant);
            }

            await SwitchTabContentShell(currentIndex, instant);
        }

        async UniTask SwitchTabContentShell(int index, bool instant)
        {
            await contentView.ShowPage(index, instant);
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
            // 子 ViewModel 的生命周期由 CharacterDetailViewModel 统一管理
            contentView.InfoPanelView.onUpgradeClick -= OpenUpgradeOrPromotePanel;
        }

        public override void OnRelease()
        {
            base.OnRelease();
            
        }
    }
}
