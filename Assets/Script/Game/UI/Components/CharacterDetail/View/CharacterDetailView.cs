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
            var contentVm = new CharacterDetailContentViewModel(viewModel.model); 
            contentView.Bind(contentVm);
            contentView.gameObject.SetActive(false);
            ExpBookMaterialInput materialInput = new ExpBookMaterialInput();
            var enhanceVm = new CharacterEnhanceViewmodel(viewModel.model,materialInput);
            enhancePanel.Bind(enhanceVm);
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
        }

        public override void OnRelease()
        {
            base.OnRelease();
        }
    }
}
