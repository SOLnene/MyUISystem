using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
    
    EnhanceRightBottomViewModel vm;
    readonly List<ItemSlotView> slotsViews = new List<ItemSlotView>();
    const string materialSlotPrefabAddress = "ui/prefab/item_slot_material";
    [SerializeField]
    GameObject switchRoot;
    [SerializeField]
    CanvasGroup switchRootGroup;
    [SerializeField]
    CanvasGroup materialContentGroup;
    [SerializeField]
    RectTransform materialContentRoot;
    [SerializeField]
    CanvasGroup[] normalFadeGroups;
    [SerializeField]
    MaterialResultFxView materialFxView;
    [SerializeField]
    float fadeDuration = 0.12f;
    [SerializeField]
    float materialEnterDuration = 0.18f;
    [SerializeField]
    float materialEnterOffsetY = 8f;
    
    Tween switchTween;
    Sequence normalSequence;
    Vector2 materialContentDefaultPos;
    bool hasMaterialContentDefaultPos;
    
    
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

    //经验条动画完成前材料的动画
    public void ShowProcessing()
    {
        CacheFxReferences();
        KillFxTweens();
        SetInteractable(false);

        if (switchRoot != null)
            switchRoot.SetActive(false);

        if (switchRootGroup != null)
            switchRootGroup.alpha = 0f;

        if (materialFxView != null)
            materialFxView.ShowLoading();
    }

    public void ShowMaxLevelText(string text)
    {
        CacheFxReferences();
        KillFxTweens();
        SetInteractable(false);

        if (switchRootGroup != null)
            switchRootGroup.alpha = 0f;

        if (switchRoot != null)
            switchRoot.SetActive(false);

        if (materialFxView != null)
            materialFxView.ShowMaxText(text);
    }

    public void ShowNormal(bool playMaterialContentFx)
    {
        CacheFxReferences();
        KillFxTweens();
        SetInteractable(true);

        if (materialFxView != null)
            materialFxView.Hide();

        if (switchRoot != null)
            switchRoot.SetActive(true);

        if (materialContentGroup != null)
        {
            materialContentGroup.alpha = 0f;
            switchTween = materialContentGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        normalSequence = DOTween.Sequence().SetUpdate(true);
        AppendNormalFadeGroups(normalSequence);

        if (playMaterialContentFx && materialContentGroup != null)
        {
            materialContentGroup.alpha = 0f;

            if (materialContentRoot != null)
                materialContentRoot.anchoredPosition = materialContentDefaultPos + new Vector2(0f, -materialEnterOffsetY);

            normalSequence.Join(materialContentGroup.DOFade(1f, materialEnterDuration).SetEase(Ease.OutQuad));

            if (materialContentRoot != null)
                normalSequence.Join(materialContentRoot.DOAnchorPos(materialContentDefaultPos, materialEnterDuration).SetEase(Ease.OutCubic));
        }
        else if (materialContentGroup != null)
        {
            materialContentGroup.alpha = 1f;
        }
    }

    void CacheFxReferences()
    {
        if (switchRootGroup == null && switchRoot != null)
            switchRootGroup = switchRoot.GetComponent<CanvasGroup>();

        if (materialContentRoot != null && !hasMaterialContentDefaultPos)
        {
            materialContentDefaultPos = materialContentRoot.anchoredPosition;
            hasMaterialContentDefaultPos = true;
        }
    }

    void AppendNormalFadeGroups(Sequence sequence)
    {
        if (normalFadeGroups == null)
            return;

        foreach (var group in normalFadeGroups)
        {
            if (group == null)
                continue;

            group.alpha = 0f;
            sequence.Join(group.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad));
        }
    }

    void SetInteractable(bool interactable)
    {
        if (switchRootGroup != null)
        {
            switchRootGroup.interactable = interactable;
            switchRootGroup.blocksRaycasts = interactable;
        }

        if (filterDropdown != null)
            filterDropdown.interactable = interactable;

        if (quickAddButton != null)
            quickAddButton.interactable = interactable;
    }

    void KillFxTweens()
    {
        if (switchTween != null)
        {
            switchTween.Kill();
            switchTween = null;
        }

        if (normalSequence != null)
        {
            normalSequence.Kill();
            normalSequence = null;
        }
    }

    void OnDestroy()
    {
        KillFxTweens();
    }
}
