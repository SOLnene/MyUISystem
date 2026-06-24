using Cysharp.Threading.Tasks;
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
        [SerializeField]
        CharacterTalentPanelView talentPanelView;
        [SerializeField]
        AnimatedPanel pageAnimPanel;
        
        public CharacterDetailInfoPanelView InfoPanelView => infoPageView;
        CharacterDetailContentViewModel vm;
        int currentPageIndex = -1;
    
        public override void Bind(CharacterDetailContentViewModel viewModel)
        {
            base.Bind(viewModel);
            Debug.Log("bind content");
            infoPageView.Bind(Vm.InfoViewModel);
            equipPageView.Bind(Vm.EquipPageViewModel);
            talentPanelView.Bind(Vm.currentCharacter.Value);
        }

        public async UniTask ShowPage(int index, bool instant)
        {
            if (currentPageIndex == index)
            {
                return;
            }

            if (currentPageIndex >= 0)
            {
                await pageAnimPanel.Hide(instant);
            }

            infoPageView.gameObject.SetActive(index == 0);
            equipPageView.gameObject.SetActive(index == 1);
            talentPanelView.gameObject.SetActive(index == 3);
            currentPageIndex = index;
            await pageAnimPanel.Show(instant);
        }
    }
}
