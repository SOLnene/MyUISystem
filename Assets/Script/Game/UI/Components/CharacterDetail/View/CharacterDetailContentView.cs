using SkierFramework;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterDetailContentView : BindableUI<CharacterDetailContentViewModel>
    {
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
        /*[ControlBinding]
        private CharacterDetailTabView tabArea;*/
        [ControlBinding]
        private CharacterDetailPreviewView characterPreviewArea;
        [ControlBinding]
        private CharacterDetailInfoPanelView infoPanelArea;

		#pragma warning restore 0649
#endregion

        CharacterDetailContentViewModel vm;
    
        public override void Bind(object viewModel)
        {
            base.Bind(viewModel);
        
            infoPanelArea.Bind(Vm.InfoViewModel);
        }
    }
}
