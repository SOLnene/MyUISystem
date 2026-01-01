    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using TMPro;
    using UniRx;
    using UnityEngine;
    using UnityEngine.Serialization;
    using UnityEngine.UI;

    public class RefinePanelView : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI refineLevelText;
        [SerializeField]
        Button quickAddButton;
        [FormerlySerializedAs("itemContent")]
        [SerializeField]
        GameObject slotContent;
        [SerializeField]
        List<ItemSlotView> slotViews;
        
        RefinePanelViewModel vm;

        CompositeDisposable disposable = new CompositeDisposable();

        ItemSlotView slotPrefab;
        public void Bind(RefinePanelViewModel viewModel)
        {
            //disposable.Dispose();
            
            vm = viewModel;
            vm.equipItem.Value.refineLevel.Subscribe(level =>
            {
                refineLevelText.text = $"{level}阶";
            }).AddTo(disposable);

            if(slotViews == null || slotViews.Count == 0)
            {
                slotViews = slotContent.GetComponentsInChildren<ItemSlotView>().ToList();
            }

            quickAddButton.onClick.AsObservable().Subscribe(_ =>
            {
                vm.OnQuickAddClicked();
            }).AddTo(disposable);
            
            //格子绑定
            for(int i=0;i<vm.slotViewModels.Count;i++)
            {
                var slotVM = vm.slotViewModels[i];
                var slotView = slotViews[i];
                slotView.Bind(slotVM);
                slotVM.onClick.Subscribe(_ =>
                {
                    vm.OnSlotClick(slotVM);
                }).AddTo(disposable);
            }
        }

        
    }
