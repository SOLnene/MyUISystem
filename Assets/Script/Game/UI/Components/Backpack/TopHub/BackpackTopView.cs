using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using UnityEngine.Serialization;

public class BackpackTopView : MonoBehaviour
{
    [Serializable]
    class BackpackCategoryButtonConfig
    {
        public ItemCategory category;
        public string displayName;
        public Sprite icon;
    }

    [Header("图片")]
    Image bgImage;
    
    
    
    [Header("UI references")]
    [SerializeField]
    Button backBtn;
    [FormerlySerializedAs("categoryText")]
    [Header("文本")]
    [SerializeField]
    TextMeshProUGUI titleText;
    
    [SerializeField]
    Transform categoryBtnParent;
    
    List<CategoryButtonView> categoryBtns = new();
    readonly CompositeDisposable bindDisposables = new();

    BackpackTopViewModel topVM;

    public IObservable<int> OnCategoryChanged => topVM.SelectedCategoryIndex;
    
    public event Action OnBackClicked;

    // UI有映射表
    [SerializeField] 
    List<BackpackCategoryButtonConfig> categoryConfigs;
    
    [SerializeField]
    CategoryButtonView categoryBtnPrefab;
    TopViewType topViewType;
    
    void Awake()
    {
        if (backBtn != null)
        {
            backBtn.onClick.AddListener(()=> OnBackClicked?.Invoke());
        }
    }

    public void Bind(BackpackTopViewModel vm)
    {
        bindDisposables.Clear();
        topVM = vm;

        InitCategoryButtons(vm.Categories, vm.SelectedCategoryIndex.Value);
        
        vm.SelectedCategoryIndex.Subscribe(index =>
        {
            UpdateSelectedButton(index);
        }).AddTo(bindDisposables);
    }
    
    public void InitCategoryButtons(List<ItemCategory> categories, int defaultIndex = 0)
    {
        EnsureDefaultCategoryConfigs();
        foreach (Transform child in categoryBtnParent)
        {
            Destroy(child.gameObject);
        }

        categoryBtns.Clear();
        for(int i=0; i<categories.Count; i++)
        {
            var btn = Instantiate(categoryBtnPrefab, categoryBtnParent);
            int index = i;
            var config = categoryConfigs.FirstOrDefault(c => c.category == categories[i]);
            btn.Init(GetDisplayName(categories[i], config), config?.icon, () => topVM.SetCategory(index));
            categoryBtns.Add(btn);
        }
        
        UpdateSelectedButton(defaultIndex);
    }
    
    void UpdateSelectedButton(int index)
    {
        for (int i = 0; i < categoryBtns.Count; i++)
        {
            categoryBtns[i].SetSelected(i == index);
        }

        var selectedCategory = topVM.Categories[index];
        var config = categoryConfigs.FirstOrDefault(c => c.category == selectedCategory);
        titleText.text = GetDisplayName(selectedCategory, config);
    }

    string GetDisplayName(ItemCategory category, BackpackCategoryButtonConfig config)
    {
        if (config != null && !string.IsNullOrEmpty(config.displayName))
        {
            return config.displayName;
        }

        return category.ToString();
    }

    void EnsureDefaultCategoryConfigs()
    {
        if (categoryConfigs != null && categoryConfigs.Count > 0)
        {
            return;
        }

        categoryConfigs = new List<BackpackCategoryButtonConfig>
        {
            new BackpackCategoryButtonConfig { category = ItemCategory.Equip },
            new BackpackCategoryButtonConfig { category = ItemCategory.Consumable },
            new BackpackCategoryButtonConfig { category = ItemCategory.Material },
            new BackpackCategoryButtonConfig { category = ItemCategory.QuestItem },
            new BackpackCategoryButtonConfig { category = ItemCategory.ExpBook },
            new BackpackCategoryButtonConfig { category = ItemCategory.All },
        };
    }
    
    void OnDestroy()
    {
        bindDisposables.Dispose();
    }
}
