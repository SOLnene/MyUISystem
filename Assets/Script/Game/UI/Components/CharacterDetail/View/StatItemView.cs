using SkierFramework;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
namespace Game.UI.Components.CharacterDetail
{
    public class StatItemView : BindableUI<StatItemViewModel>
    {
    #region 控件绑定变量声明，自动生成请勿手改
    		#pragma warning disable 0649
    		[ControlBinding]
    		private Image icon;
    		[ControlBinding]
    		private TextMeshProUGUI label;
    		[ControlBinding]
    		private TextMeshProUGUI value;
    		[ControlBinding]
    		private GameObject nextArrow;
    		[ControlBinding]
    		private GameObject nextGroup;
    		[ControlBinding]
    		private TextMeshProUGUI nextValue;
    
    		#pragma warning restore 0649
    #endregion
    


        CompositeDisposable disposable = new CompositeDisposable();
        public override void Bind(object data)
        {
            base.Bind(data);
            disposable.Clear();
            if (Vm.icon != null)
            {
	            icon.sprite = Vm.icon;
            }
            label.text = Vm.label;
            Vm.valueText.Subscribe(text =>
            {
                value.text = text;
            }).AddTo(disposable);

            Vm.nextValueText.Subscribe(
	            text =>
	            {
		            nextValue.text = text;
	            }).AddTo(disposable);
            
            Vm.IsUpgrade.Subscribe(
	            b =>
	            {
		            nextArrow.SetActive(b);
		            nextGroup.SetActive(b);
		            value.horizontalAlignment = b ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
	            }).AddTo(disposable);
        }
        
        //为了兼容
        public override void Bind(StatItemViewModel data)
        {
            Bind((object)data);
        }
        
        public void OnDestroy()
        {
            disposable.Dispose();
        }
    }
}
