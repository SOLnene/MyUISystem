using System.Collections.Generic;
using SkierFramework;
using UnityEngine;
namespace Game.UI.Components.CharacterDetail
{
    public class CharacterEnhancePanel : BindableUI<CharacterEnhanceViewmodel>
    {
      #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
	    [ControlBinding]
	    private BindableUI upgradePanel;
	    [ControlBinding]
	    private BindableUI[] statItems;
	    [ControlBinding]
	    private BindableUI materialPanel;

		#pragma warning restore 0649
#endregion



	    
        public override void Bind(object viewmodel)
        {
            base.Bind(viewmodel);
            upgradePanel.Bind(Vm.previewVm);
            for (int i = 0; i < statItems.Length; i++)
            {
	            statItems[i].Bind(Vm.statItemViewModels[i]);
            }
            materialPanel.Bind(Vm);
        }
    }
}
