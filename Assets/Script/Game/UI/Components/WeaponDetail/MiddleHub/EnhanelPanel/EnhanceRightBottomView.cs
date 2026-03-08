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
    [SerializeField]
    ItemSlotView slotPrefab;

    EnhanceRightBottomViewModel vm;
    readonly List<ItemSlotView> slotsViews = new List<ItemSlotView>();
    
    
    public void Bind(EnhanceRightBottomViewModel viewModel)
    {
        vm = viewModel;
        slotPrefab = UIManager.Instance.slotPrefab;
        // 绑定消耗文本
        vm.currentConsume.Subscribe(value =>
        {
            consumText.text = $"消耗数量: {value}/{vm.maxConsume}";
        }).AddTo(this);

        // 绑定筛选下拉框
        filterDropdown.ClearOptions();
        List<string> options = new List<string>();
        filterDropdown.AddOptions(vm.availabelLevels.ConvertAll(level=>level.ToString()));
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

        // 初始化ItemSlots
        /*for (int i = 0; i < vm.slots.Count; i++)
        {
            ItemSlotView slotView = Instantiate(slotPrefab, slotParent);
            slotView.Bind(vm.slots[i]);
            slotsViews.Add(slotView);
        }*/
        RefreshSlots();
        
        vm.slotViewModels.ObserveAdd().Subscribe(add => AddSlot(add.Value)).AddTo(this);
        vm.slotViewModels.ObserveRemove().Subscribe(remove => RemoveSlot(remove.Value)).AddTo(this);    
    }

    void AddSlot(ItemSlotViewModel slotVM)
    {
        var slotView = Instantiate(slotPrefab, slotParent);
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
        foreach (var slotView in slotsViews)
        {
            Destroy(slotView.gameObject);
        }
        slotsViews.Clear();
        foreach (var slotVM in vm.slotViewModels)
        {
            var slotView = Instantiate(slotPrefab, slotParent);
            slotsViews.Add(slotView);
            slotView.Bind(slotVM);
            slotVM.onClick.Subscribe(_ => vm.OnSlotClick(slotVM)).AddTo(this);
        }
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
            var slotView = await ItemFactory.InstantiateItemSlot(slotVM, slotParent);
            slotsViews.Add(slotView);
            slotView.Bind(slotVM);
            slotVM.onClick.Subscribe(_ => vm.OnSlotClick(slotVM)).AddTo(this);
        }
    }
}
