using System.Collections;
using System.Collections.Generic;
using Game.UI.Components.CharacterDetail;
using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CharacterMaterialView : BindableUI<CharacterEnhanceViewmodel>
{
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private ItemSlotView[] items;
    [ControlBinding]
    private Button addBtn;
    [ControlBinding]
    private Button removeBtn;
    [ControlBinding]
    private TextMeshProUGUI countText;
    [ControlBinding]
    private Button quickAddButton;
    [ControlBinding]
    private TextMeshProUGUI costText;
    [ControlBinding]
    private Button upgradeBtn;

		#pragma warning restore 0649
#endregion

	public override void Bind(object data)
	{
		base.Bind(data);
		BindBooks();
		BindAddRemove();
		BindCost();
	}

	
	void BindBooks()
	{
		for (int i = 0; i < items.Length; i++)
		{
			items[i].Bind(Vm.itemViewModels[i]);
		}
	}

	void BindAddRemove()
	{
		addBtn.onClick.RemoveAllListeners();
		addBtn.onClick.AddListener(() =>
		{
			//todo:判断数量
			Vm.AddBook(Vm.selectedBook.Value);
		});
		removeBtn.onClick.AddListener(() =>
		{
			Vm.RemoveBook(Vm.selectedBook.Value);
		});
		Observable.Merge(Observable.Merge(Vm.materialInput.Counts.Values).Select(_ => Unit.Default)
				, Vm.selectedBook.Select(_ => Unit.Default))
			.Subscribe(_ =>
			{
				countText.text = Vm.GetCurrentBookCount().ToString();
			}).AddTo(this);
	}

	void BindCost()
	{
		Vm.materialInput.TotalGoldRp
			.Subscribe(value => costText.text = value.ToString())
			.AddTo(this);
		upgradeBtn.onClick.RemoveAllListeners();
		upgradeBtn.onClick.AddListener((() => Vm.ConfirmEnhance()));
	}
}
