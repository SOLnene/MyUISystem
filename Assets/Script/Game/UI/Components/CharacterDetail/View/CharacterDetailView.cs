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
        [ControlBinding]
        private Button[] TabItem;

		#pragma warning restore 0649
#endregion



        
        private const float TOP_BAR_HEIGHT = 150f;     // 根据设计稿改
        private const float BOTTOM_BAR_HEIGHT = 140f;

        CharacterDetailViewModel vm;
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
            contentView.gameObject.SetActive(false);

           
            // 初始化时先决定显示哪个面板
            RefreshUpgradeOrPromotePanel();

            // 升级后/突破后重新判断一次
            viewModel.enhanceViewmodel.onUpgrade
                .Subscribe(_ => RefreshUpgradeOrPromotePanel())
                .AddTo(disposable);

            viewModel.promoteViewmodel.onPromote
                .Subscribe(_ => RefreshUpgradeOrPromotePanel())
                .AddTo(disposable);
            SetTabItems();
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
            for (int i = 0; i < TabItem.Length; i++)
            {
                int index = i;
                TabItem[i].onClick.RemoveAllListeners();
               TabItem[i].onClick.AddListener(
                   ()=>ModelViewer.Instance.SwitchToPreset(ModelViewer.Instance.presets[index]));
            }
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
