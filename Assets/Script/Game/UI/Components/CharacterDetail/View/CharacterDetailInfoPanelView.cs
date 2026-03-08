using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
namespace Game.UI.Components.CharacterDetail
{
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
		[ControlBinding]
		private BarBase favorBar;

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
        
			var attributeViewmodel = new AttributePageViewModel(model);
        
			for (int i = 0; i < statItems.Length; i++)
			{
				statItems[i].Bind(attributeViewmodel.stats[i]);
			}

			model.Stats.Favor.Subscribe(
				favor =>
				{
					favorBar.SetValue((int)favor,100);
				}).AddTo(disposable);
		}

		protected override void AfterBind()
		{
        
		}

		public void OnDestroy()
		{
			disposable.Dispose();
		}
	}
}
