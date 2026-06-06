using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UniRx;
using UnityEngine;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterEnhancePanel : BindableUI<CharacterEnhanceViewmodel>
    {
      #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	    [ControlBinding]
	    private EnhanceLevelPreviewView upgradePanel;
	    [ControlBinding]
	    private BindableUI[] statItems;
	    [ControlBinding]
	    private BindableUI materialPanel;

		#pragma warning restore 0649
#endregion


	    [SerializeField]
	    UITopBar topBar;
	    [SerializeField]
	    AnimatedPanel topPanel;
	    [SerializeField]
	    AnimatedPanel infoPanel;
	    
        public override void Bind(object viewmodel)
        {
            base.Bind(viewmodel);
            topBar.Bind(Vm.modelName, new ReactiveProperty<int>(10000), Vm.onBack);
            upgradePanel.Bind(Vm.previewVm);
            for (int i = 0; i < statItems.Length; i++)
            {
	            statItems[i].Bind(Vm.statItemViewModels[i]);
            }
            materialPanel.Bind(Vm);
        }

        public async UniTask ShowPanel(bool instant)
        {
            gameObject.SetActive(true);
            await UniTask.WhenAll(
                topPanel.Show(instant),
                infoPanel.Show(instant)
            );
        }

        public async UniTask HidePanel(bool instant)
        {
            await UniTask.WhenAll(
                topPanel.Hide(instant),
                infoPanel.Hide(instant)
            );
            gameObject.SetActive(false);
        }
    }
}
