using System;
using System.Collections;
using System.Collections.Generic;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailInfoPanelView : BindableUI
{
    #region 控件绑定变量声明，自动生成请勿手改
    		#pragma warning disable 0649
    		[ControlBinding]
    		private TextMeshProUGUI nameText;
    		[ControlBinding]
    		private Image[] starImg;
    		[ControlBinding]
    		private TextMeshProUGUI levelText;
    		[ControlBinding]
    		private Button detailBtn;
    		[ControlBinding]
    		private TextMeshProUGUI descriptionText;
    		[ControlBinding]
    		private TextMeshProUGUI expText;
    		[ControlBinding]
    		private Image expFill;
    		[ControlBinding]
    		private BindableUI[] statItems;
    
    		#pragma warning restore 0649
    #endregion
    










    CompositeDisposable disposable = new CompositeDisposable();

    CharacterDetailInfoViewModel vm;
    public void Bind(CharacterDetailInfoViewModel viewModel)
    {
        vm = viewModel;
        var model = viewModel.model;
        nameText.text = model.Name.Value;
        disposable.Clear();
        for (int i = 0; i < starImg.Length; i++)
        {
            starImg[i].gameObject.SetActive(i < viewModel.model.Star.Value);
        }

        vm.model.LevelRP.Subscribe(level =>
        {
            levelText.text = $"Lv.{level}";
        }).AddTo(disposable);

        vm.model.Description.Subscribe(desc =>
        {
            descriptionText.text = desc;
        }).AddTo(disposable);
        
        vm.ExpText.Subscribe(
            text =>
            {
                expText.text = text;
            }).AddTo(disposable);
        vm.ExpProgress.Subscribe(
            progress =>
            {
                expFill.fillAmount = Mathf.Max(0.001f,progress);
            }).AddTo(disposable);
        Debug.Log(vm.model.LevelRP.Value);
        Debug.Log(vm.model.ExpRP.Value);
        Debug.Log(vm.model.LevelSystem.NextLevelExp);
    }

    protected override void AfterBind()
    {
        
    }

    public void OnDestroy()
    {
        disposable.Dispose();
    }
}
