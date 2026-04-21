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
        private RectTransform bottomLayer;
        [ControlBinding]
        private TextMeshProUGUI charaText;
        [ControlBinding]
        private CharacterDetailContentView contentView;
        [ControlBinding]
        private Image final;
        [ControlBinding]
        private BindableUI enhancePanel;
        [ControlBinding]
        private BindableUI promotePanel;
        

		#pragma warning restore 0649
#endregion

        [SerializeField]
        CharacterDetailTabItem[] tabItems;

        
        private const float TOP_BAR_HEIGHT = 150f;     // 根据设计稿改
        private const float BOTTOM_BAR_HEIGHT = 140f;

        CharacterDetailViewModel vm;

        int currentIndex = -1;
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
            viewModel.model.Name
                .Subscribe(name =>
                {
                    charaText.text = name;
                }).AddTo(disposable);
            final.gameObject.SetActive(false);
            contentView.Bind(viewModel.contentViewModel);
           
            enhancePanel.Bind(viewModel.enhanceViewmodel);
            promotePanel.Bind(viewModel.promoteViewmodel);
            contentView.gameObject.SetActive(true);
            enhancePanel.gameObject.SetActive(false);
            promotePanel.gameObject.SetActive(false);
            // 初始化时先决定显示哪个面板
            //RefreshUpgradeOrPromotePanel();

            // 升级后/突破后重新判断一次
            viewModel.enhanceViewmodel.onUpgrade
                .Subscribe(_ => RefreshUpgradeOrPromotePanel())
                .AddTo(disposable);

            viewModel.promoteViewmodel.onPromote
                .Subscribe(_ => RefreshUpgradeOrPromotePanel())
                .AddTo(disposable);
            SetTabItems();
            //初始化为idle
            //todo:切换角色初始化
            //todo:不写死
            SetCurrentTab(0, true);
            //todo:如果脸部动画很明显，需要给facepreset也做一个immediate方法
        }
    
        void RefreshUpgradeOrPromotePanel()
        {
            // 规则：小于当前 rank 最大等级 -> 升级；达到/超过 -> 突破
            int level = vm.model.LevelRP.Value;
            int maxLevel = vm.model.GetMaxLevel();
            bool showUpgrade = level < maxLevel;

            enhancePanel.gameObject.SetActive(showUpgrade);
            promotePanel.gameObject.SetActive(!showUpgrade);
        }

        void SetTabItems()
        {
            if (tabItems == null || tabItems.Length == 0)
            {
                return;
            }

            for (int i = 0; i < tabItems.Length; i++)
            {
                int index = i;
                tabItems[i].Bind(i, ()=>
                {
                    OnTabClicked(index);
                });
            }
        }
        
        void OnTabClicked(int index)
        {
            if (currentIndex >= -1)
            {
                SetCurrentTab(index, false);
                return;
            }

            // 取消旧选中
            if (currentIndex >= 0)
            {
                var oldTab = tabItems[currentIndex];
                oldTab.SetSelected(false);
            }

            // 设置新选中
            currentIndex = index;
            var newTab = tabItems[currentIndex];
            newTab.SetSelected(true);
            
            //SwitchContent(index);
            
            ModelViewer.Instance.SwitchToPreset(ModelViewer.Instance.presets[index]);
            ModelViewer.Instance.SwitchFacePreset(ModelViewer.Instance.facePresets[index]);
        }

        void SetCurrentTab(int index, bool instant)
        {
            if (tabItems == null || tabItems.Length == 0)
            {
                ApplyTabPresentation(index, instant);
                currentIndex = index;
                return;
            }

            if (index < 0 || index >= tabItems.Length)
            {
                return;
            }

            if (currentIndex == index && currentIndex >= 0)
            {
                tabItems[currentIndex].SetSelected(true, instant);
                ApplyTabPresentation(index, instant);
                return;
            }

            if (currentIndex >= 0 && currentIndex < tabItems.Length)
            {
                tabItems[currentIndex].SetSelected(false, instant);
            }

            currentIndex = index;
            tabItems[currentIndex].SetSelected(true, instant);
            ApplyTabPresentation(index, instant);
        }

        void ApplyTabPresentation(int index, bool immediate)
        {
            if (ModelViewer.Instance == null)
            {
                return;
            }

            if (ModelViewer.Instance.presets != null && index >= 0 && index < ModelViewer.Instance.presets.Length)
            {
                ModelViewer.Instance.SwitchToPreset(ModelViewer.Instance.presets[index], immediate);
            }

            if (ModelViewer.Instance.facePresets != null && index >= 0 && index < ModelViewer.Instance.facePresets.Length)
            {
                ModelViewer.Instance.SwitchFacePreset(ModelViewer.Instance.facePresets[index]);
            }

            SwitchTabContentShell(index);
        }

        void SwitchTabContentShell(int index)
        {
            // todo: tab 对应页面未接入，先保留空壳入口
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
        }

        public override void OnRelease()
        {
            base.OnRelease();
            
        }
    }
}
