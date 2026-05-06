using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class EnhanceRightBottomView : MonoBehaviour
{
    [Header("显示消耗数量文本")]
    [SerializeField]
    TextMeshProUGUI consumText;
    [Header("筛选下拉框")]
    [SerializeField]
    TMP_Dropdown filterDropdown;
    [Header("快捷放入按钮")]
    [SerializeField]
    Button quickAddButton;
    [Header("ItemSlots")]
    [SerializeField]
    Transform slotParent;

    EnhanceRightBottomViewModel vm;
    readonly List<ItemSlotView> slotsViews = new List<ItemSlotView>();
    const string materialSlotPrefabAddress = "ui/prefab/item_slot_material";
    
    
    public void Bind(EnhanceRightBottomViewModel viewModel)
    {
        vm = viewModel;
        // 绑定消耗文本
        vm.currentConsume.Subscribe(value =>
        {
            consumText.text = $"装备强化消耗({value}/{vm.maxConsume})";
        }).AddTo(this);

        // 绑定筛选下拉框
        filterDropdown.ClearOptions();
        List<string> options = new List<string>();
        filterDropdown.AddOptions(vm.availabelLevels.ConvertAll(level => $"{level}星及以下素材"));
        filterDropdown.onValueChanged
            .AsObservable()
            .Subscribe(index =>
            {
                vm.FilterByLevel(vm.availabelLevels[index]);
            }).AddTo(this);

        // 绑定快捷放入按钮
        quickAddButton.onClick
            .AsObservable()
            .Subscribe(_ =>
            {
                vm.OnQuickAddClicked();
            }).AddTo(this);
        
        RefreshSlots();
        
        vm.slotViewModels.ObserveAdd().Subscribe(add => AddSlot(add.Value)).AddTo(this);
        vm.slotViewModels.ObserveRemove().Subscribe(remove => RemoveSlot(remove.Value)).AddTo(this);    
    }

    void AddSlot(ItemSlotViewModel slotVM)
    {
        AddSlotAsync(slotVM).Forget();
    }

    async UniTask AddSlotAsync(ItemSlotViewModel slotVM)
    {
        var slotView = await ItemFactory.InstantiateItemSlot(slotVM, slotParent, materialSlotPrefabAddress);
        if (slotView == null)
        {
            return;
        }

        slotsViews.Add(slotView);
        slotView.Bind(slotVM);
        slotVM.onClick.Subscribe(_ => vm.OnSlotClick(slotVM)).AddTo(this);
    }
    
    void RemoveSlot(ItemSlotViewModel slotVM)
    {
        var slotView = slotsViews.Find(s => s.vm == slotVM);
        if (slotView != null)
        {
            slotsViews.Remove(slotView);
            Destroy(slotView.gameObject);
        }
    }
    
    /// <summary>
    /// 同步刷新ItemSlots,之后换做加载占位符
    /// </summary>
    void RefreshSlots()
    {
        RefreshSlotsAsync().Forget();
    }
    
    /// <summary>
    /// 异步刷新ItemSlots，之后更复杂的时候再使用
    /// </summary>
    async UniTask RefreshSlotsAsync()
    {
        foreach (var slotView in slotsViews)
        {
            Destroy(slotView.gameObject);
        }
        slotsViews.Clear();
        foreach (var slotVM in vm.slotViewModels)
        {
            var slotView = await ItemFactory.InstantiateItemSlot(slotVM, slotParent, materialSlotPrefabAddress);
            if (slotView == null)
            {
                continue;
            }

            slotsViews.Add(slotView);
            slotView.Bind(slotVM);
            slotVM.onClick.Subscribe(_ => vm.OnSlotClick(slotVM)).AddTo(this);
        }
    }
}
