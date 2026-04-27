using SkierFramework;
using UnityEngine;
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
        private CharacterDetailInfoPanelView infoPageView;

		#pragma warning restore 0649
#endregion
        [SerializeField]
        CharacterDetailEquipPageView equipPageView;
        
        public CharacterDetailInfoPanelView InfoPanelView => infoPageView;
        CharacterDetailContentViewModel vm;
    
        public override void Bind(CharacterDetailContentViewModel viewModel)
        {
            base.Bind(viewModel);
            Debug.Log("bind content");
            infoPageView.Bind(Vm.InfoViewModel);
            equipPageView.Bind(Vm.EquipPageViewModel);
        }

        public void ShowPage(int index)
        {
            infoPageView.gameObject.SetActive(index == 0);
            equipPageView.gameObject.SetActive(index == 1);
        }
    }
}
