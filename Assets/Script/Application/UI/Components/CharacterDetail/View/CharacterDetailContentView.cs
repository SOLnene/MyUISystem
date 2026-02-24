using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using UnityEngine;

public class CharacterDetailContentView : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private CharacterDetailTabView tabArea;
    [ControlBinding]
    private CharacterDetailPreviewView characterPreviewArea;
    [ControlBinding]
    private CharacterDetailInfoPanelView infoPanelArea;

		#pragma warning restore 0649
#endregion

    CharacterDetailContentViewModel vm;
    
    public void Bind(CharacterDetailContentViewModel viewModel)
    {
        vm = viewModel;
        
        infoPanelArea.Bind(viewModel.InfoViewModel);
    }
}
