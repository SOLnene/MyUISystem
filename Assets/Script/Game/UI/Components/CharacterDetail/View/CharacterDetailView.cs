using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Character;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
namespace Game.UI.Components.CharacterDetail
{
    public partial class CharacterDetailView : UIView
    {
        enum CameraPushStartAnchor
        {
            EquipEnterStart,
            CharacterExitEnd
        }

        enum DetailOperation
        {
            None,
            SwitchingTab,
            SwitchingCharacter,
            PlayingResult,
            NavigatingToEquip
        }

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
        [SerializeField]
        CameraPreset equipEnhancePushPreset;
        [SerializeField]
        CameraPushStartAnchor cameraPushStartAnchor;
        [SerializeField]
        [Tooltip("负数提前，正数延后，单位为秒")]
        float cameraPushStartOffsetSeconds;
        private const float TOP_BAR_HEIGHT = 150f;   
        private const float BOTTOM_BAR_HEIGHT = 140f;


        //tab标签
        static readonly string[] Labels =
        {
            "属性",
            "装备",
            "圣遗物",
            "天赋"
        };
        const int DefaultOpenTabIndex = 0;
          
        CharacterDetailViewModel vm;

        int currentIndex = -1;
        DetailOperation currentOperation;
        int operationVersion;
        bool isClosing;
        bool isTalentDetailMode;
        CancellationTokenSource equipEnhanceTransitionCancellation;
        CompositeDisposable disposable = new CompositeDisposable();
        CompositeDisposable characterDisposable = new CompositeDisposable();

        // 页面操作结束不代表模型已完成交接；模型侧的状态仍由 ModelViewer 管理。
        bool CanStartPresentationChange =>
            !isClosing
            && currentOperation == DetailOperation.None
            && (ModelViewer.Instance == null || !ModelViewer.Instance.IsInTransition);

        int BeginOperation(DetailOperation operation)
        {
            currentOperation = operation;
            return ++operationVersion;
        }

        void CompleteOperation(int version)
        {
            // 关闭或重新打开后，旧任务的 finally 不得解除新操作的限制。
            if (version == operationVersion)
                currentOperation = DetailOperation.None;
        }

        public override void OnInit(UIControlData uiControlData,UIViewHandle handle)
        {
            base.OnInit(uiControlData,handle);
        }

        public override void OnOpen(object data)
        {
            base.OnOpen(data);
            ++operationVersion;
            currentOperation = DetailOperation.None;
            CancelEquipEnhanceTransition();
            ModelViewer.Instance.PlayStarFieldParticles();
            isClosing = false;
            vm = data as CharacterDetailViewModel;
            Bind(vm);
            ShowInitialCharacterPreviewAsync(vm.model).Forget();
        }

        public override void OnResume()
        {
            base.OnResume();
            bool returningFromEquip = currentOperation == DetailOperation.NavigatingToEquip;
            bool restoredFromEquip =
                returningFromEquip
                && ModelViewer.Instance.IsCharacterPreviewActive;
            if (returningFromEquip)
                CompleteOperation(operationVersion);

            if (!restoredFromEquip && vm != null && !isClosing)
            {
                ShowInitialCharacterPreviewAsync(vm.model).Forget();
            }

            if (returningFromEquip)
            {
                ShowMainPanels(false).Forget();
            }
        }

        public void Bind(CharacterDetailViewModel viewModel)
        {
            disposable.Clear();
            vm = viewModel;

            BindCharacterViews();

            viewModel.RequestRebindCharacter
                .Subscribe(_ => BindCharacterViews())
                .AddTo(disposable);

            viewModel.onBackToMain
                .Subscribe(_ => BackToDetailMainView())
                .AddTo(disposable);

            SetTabItems();
            ShowMainPanels(false).Forget();
            //todo:如果脸部动画很明显，需要给facepreset也做一个immediate方法
        }

        void BindCharacterViews()
        {
            characterDisposable.Clear();
            contentView.InfoPanelView.onUpgradeClick -= OpenUpgradeOrPromotePanel;
            contentView.TalentPanelView.TalentDetailOpened -= OpenTalentDetailMode;
            contentView.TalentPanelView.TalentDetailClosed -= CloseTalentDetailMode;
            topView.Bind(vm.model.Name.Value, vm.OwnedCharacters, vm.model, RequestCharacterSwitch, OnCancel);
            
            final.gameObject.SetActive(false);
            contentView.Bind(vm.contentViewModel);
           
            enhancePanelView.Bind(vm.enhanceViewmodel);
            promotePanelView.Bind(vm.promoteViewmodel);
            BackToDetailMainViewImmediate();
            // 初始化时先决定显示哪个面板
            //RefreshUpgradeOrPromotePanel();

            // 升级后/突破后重新判断一次
            vm.enhanceViewmodel.requestPlayEnhanceResult
                .Subscribe(result => PlayEnhanceResultFlow(result).Forget())
                .AddTo(characterDisposable);

            vm.promoteViewmodel.requestPlayPromoteResult
                .Subscribe(result => PlayPromoteResultFlow(result).Forget())
                .AddTo(characterDisposable);

            contentView.InfoPanelView.onUpgradeClick += OpenUpgradeOrPromotePanel;
            contentView.TalentPanelView.TalentDetailOpened += OpenTalentDetailMode;
            contentView.TalentPanelView.TalentDetailClosed += CloseTalentDetailMode;

            //初始化为idle
            //todo:切换角色初始化
            //todo:不写死
            for (int i = 0; i < tabItems.Length; i++)
            {
                tabItems[i].SetSelected(false, true);
            }

            currentIndex = -1;
            SwitchTab(DefaultOpenTabIndex, true);
        }

        void RequestCharacterSwitch(CharacterModel character)
        {
            if (character == null || character == vm.model || !CanStartPresentationChange)
            {
                return;
            }

            SwitchCharacterAsync(character).Forget();
        }

        async UniTask SwitchCharacterAsync(CharacterModel character)
        {
            int version = BeginOperation(DetailOperation.SwitchingCharacter);
            try
            {
                bool previewReady = await ModelViewer.Instance.TryShowPreviewAsync(
                    ModelPreviewType.Character,
                    character.Definition.key);
                if (!previewReady || isClosing || vm == null || version != operationVersion)
                    return;

                vm.SelectCharacter(character);
            }
            finally
            {
                CompleteOperation(version);
            }
        }

        async UniTask ShowInitialCharacterPreviewAsync(CharacterModel character)
        {
            bool ownsOperation = currentOperation == DetailOperation.None;
            int version = ownsOperation
                ? BeginOperation(DetailOperation.SwitchingCharacter)
                : operationVersion;
            try
            {
                await ModelViewer.Instance.TryShowPreviewAsync(
                    ModelPreviewType.Character,
                    character.Definition.key);
            }
            finally
            {
                if (ownsOperation)
                    CompleteOperation(version);
            }
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
            isTalentDetailMode = false;
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

        void OpenTalentDetailMode()
        {
            SetTalentDetailMode(true, false).Forget();
        }

        void CloseTalentDetailMode()
        {
            SetTalentDetailMode(false, false).Forget();
        }

        async UniTask SetTalentDetailMode(bool active, bool instant)
        {
            if (isTalentDetailMode == active)
            {
                return;
            }

            isTalentDetailMode = active;
            if (active)
            {
                await UniTask.WhenAll(
                    topPanel.Hide(instant),
                    tabPanel.Hide(instant)
                );
                return;
            }

            await UniTask.WhenAll(
                topPanel.Show(instant),
                tabPanel.Show(instant)
            );
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
            if (isClosing || currentOperation != DetailOperation.None)
                return;

            int version = BeginOperation(DetailOperation.PlayingResult);
            try
            {
                enhancePanelView.ShowEnhanceProcessing();
                await enhancePanelView.PlayEnhanceExpProgress(result);
                if (isClosing || version != operationVersion)
                    return;

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

                await enhancePanelView.PlayEnhanceLevelResult(result, () =>
                {
                    if (!isClosing && version == operationVersion)
                        onNewLevelShown();
                });
                if (isClosing || version != operationVersion)
                    return;

                if (result.needSwitchContent)
                    await RefreshUpgradeOrPromotePanelAsync(false);
            }
            finally
            {
                CompleteOperation(version);
            }
        }

        async UniTask PlayPromoteResultFlow(PromoteLevelResultData result)
        {
            if (isClosing || currentOperation != DetailOperation.None)
                return;

            int version = BeginOperation(DetailOperation.PlayingResult);
            try
            {
                promotePanelView.ShowPromoteProcessing();

                Action onNewStateShown = () => promotePanelView.ShowPromoteResultText("突破成功");

                await promotePanelView.PlayPromoteResult(result, () =>
                {
                    if (!isClosing && version == operationVersion)
                        onNewStateShown();
                });
                if (isClosing || version != operationVersion)
                    return;
                await RefreshUpgradeOrPromotePanelAsync(false);
            }
            finally
            {
                CompleteOperation(version);
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

            return CanStartPresentationChange;
        }

        void SwitchTab(int index, bool instant)
        {
            SwitchTabAsync(index, instant).Forget();
        }

        async UniTask SwitchTabAsync(int index, bool instant)
        {
            // 切角色时会内部重置默认页，这个子步骤不能覆盖外层角色切换的状态。
            bool ownsOperation = currentOperation == DetailOperation.None;
            int version = ownsOperation
                ? BeginOperation(DetailOperation.SwitchingTab)
                : operationVersion;
            try
            {
                if (currentIndex >= 0 && currentIndex < tabItems.Length)
                {
                    tabItems[currentIndex].SetSelected(false, instant);
                }

                currentIndex = index;
                tabItems[currentIndex].SetSelected(true, instant);
                await ApplyCurrentTab(instant);
            }
            finally
            {
                if (ownsOperation)
                    CompleteOperation(version);
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

        public override void OnCancel()
        {
            if (isClosing || currentOperation == DetailOperation.NavigatingToEquip)
                return;

            CloseWithTransition().Forget();
        }

        internal void OpenEquipEnhance(EquipItemViewModel weapon)
        {
            if (weapon == null || !CanStartPresentationChange)
                return;

            CancelEquipEnhanceTransition();
            var transitionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            equipEnhanceTransitionCancellation = transitionCancellation;
            NavigateToEquipEnhanceAsync(weapon, transitionCancellation).Forget(Debug.LogException);
        }

        async UniTask NavigateToEquipEnhanceAsync(
            EquipItemViewModel weapon,
            CancellationTokenSource transitionCancellation)
        {
            int version = BeginOperation(DetailOperation.NavigatingToEquip);
            CancellationToken cancellationToken = transitionCancellation.Token;
            bool completed = false;

            try
            {
                ModelViewer.Instance.PrepareEquipPreviewForCharacterReturn(
                    weapon.Model.Key);
                if (cameraPushStartAnchor == CameraPushStartAnchor.CharacterExitEnd)
                {
                    await PlayCharacterExitAnchoredTransitionAsync(weapon, cancellationToken);
                }
                else
                {
                    await PlayEquipEnterAnchoredTransitionAsync(weapon, cancellationToken);
                }
                // 成功跳转后保持导航状态，直到 OnResume 恢复角色界面。
                completed = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (version == operationVersion)
                    ModelViewer.Instance.CancelPendingPreviewLoad();
            }
            finally
            {
                if (!completed)
                    CompleteOperation(version);
                if (equipEnhanceTransitionCancellation == transitionCancellation)
                {
                    equipEnhanceTransitionCancellation = null;
                    transitionCancellation.Dispose();
                }
            }
        }

        async UniTask PlayCharacterExitAnchoredTransitionAsync(
            EquipItemViewModel weapon,
            CancellationToken cancellationToken)
        {
            UniTask hideTask =
                HideMainPanels(false).AttachExternalCancellation(cancellationToken);
            UniTask cameraPushTask;

            if (cameraPushStartOffsetSeconds < 0f)
            {
                float startDelay = Mathf.Max(
                    0f,
                    GetMainPanelTransitionDuration() + cameraPushStartOffsetSeconds);
                await UniTask.Delay(
                    TimeSpan.FromSeconds(startDelay),
                    cancellationToken: cancellationToken);
                cameraPushTask = ModelViewer.Instance.PlayCameraTransitionAsync(
                    equipEnhancePushPreset,
                    0f,
                    cancellationToken);
                await hideTask;
            }
            else
            {
                await hideTask;
                cameraPushTask = ModelViewer.Instance.PlayCameraTransitionAsync(
                    equipEnhancePushPreset,
                    cameraPushStartOffsetSeconds,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            UIManager.Instance.Open(
                UIType.EquipDetailView,
                new EquipDetailOpenParams(weapon, WeaponDetailTab.Enhance));
            await cameraPushTask;
        }

        async UniTask PlayEquipEnterAnchoredTransitionAsync(
            EquipItemViewModel weapon,
            CancellationToken cancellationToken)
        {
            await HideMainPanels(false).AttachExternalCancellation(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            UniTask cameraPushTask;
            if (cameraPushStartOffsetSeconds < 0f)
            {
                cameraPushTask = ModelViewer.Instance.PlayCameraTransitionAsync(
                    equipEnhancePushPreset,
                    0f,
                    cancellationToken);
                await UniTask.Delay(
                    TimeSpan.FromSeconds(-cameraPushStartOffsetSeconds),
                    cancellationToken: cancellationToken);
                UIManager.Instance.Open(
                    UIType.EquipDetailView,
                    new EquipDetailOpenParams(weapon, WeaponDetailTab.Enhance));
            }
            else
            {
                UIManager.Instance.Open(
                    UIType.EquipDetailView,
                    new EquipDetailOpenParams(weapon, WeaponDetailTab.Enhance));
                cameraPushTask = ModelViewer.Instance.PlayCameraTransitionAsync(
                    equipEnhancePushPreset,
                    cameraPushStartOffsetSeconds,
                    cancellationToken);
            }

            await cameraPushTask;
        }

        float GetMainPanelTransitionDuration()
        {
            return Mathf.Max(
                topPanel.TransitionDuration,
                tabPanel.TransitionDuration,
                infoPanel.TransitionDuration);
        }

        void CancelEquipEnhanceTransition()
        {
            var transitionCancellation = equipEnhanceTransitionCancellation;
            equipEnhanceTransitionCancellation = null;
            transitionCancellation?.Cancel();
            transitionCancellation?.Dispose();
        }

        async UniTask CloseWithTransition()
        {
            isClosing = true;
            int version = ++operationVersion;
            currentOperation = DetailOperation.None;
            ModelViewer.Instance.CancelPendingPreviewLoad();

            if (enhancePanelView.gameObject.activeSelf || promotePanelView.gameObject.activeSelf)
            {
                await HideUpgradeOrPromotePanel(false);
            }
            else
            {
                await HideMainPanels(false);
            }

            if (version == operationVersion)
                base.OnCancel();
        }
        
        
        public override void OnClose()
        {
            isClosing = true;
            ++operationVersion;
            currentOperation = DetailOperation.None;
            CancelEquipEnhanceTransition();
            ModelViewer.Instance.CancelPendingPreviewLoad();
            ModelViewer.Instance.StopStarFieldParticles();
            base.OnClose();
            // 子 ViewModel 的生命周期由 CharacterDetailViewModel 统一管理
            characterDisposable.Clear();
            disposable.Clear();
            contentView.InfoPanelView.onUpgradeClick -= OpenUpgradeOrPromotePanel;
            contentView.TalentPanelView.TalentDetailOpened -= OpenTalentDetailMode;
            contentView.TalentPanelView.TalentDetailClosed -= CloseTalentDetailMode;
        }

        public override void OnRelease()
        {
            isClosing = true;
            ++operationVersion;
            currentOperation = DetailOperation.None;
            CancelEquipEnhanceTransition();
            base.OnRelease();
            characterDisposable.Dispose();
            disposable.Dispose();
            
        }
    }
}
