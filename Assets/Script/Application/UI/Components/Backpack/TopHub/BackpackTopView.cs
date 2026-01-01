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
    
    [Header("Config")]
    [SerializeField]
    CategoryButtonView categoryBtnPrefab;

    
    
    List<CategoryButtonView> categoryBtns = new();

    BackpackTopViewModel topVM;

    public IObservable<int> OnCategoryChanged => topVM.SelectedCategoryIndex;
    
    public event Action OnBackClicked;

    // UI有映射表
    [SerializeField] 
    List<CategoryConfig> categoryConfigs;

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
        topVM = vm;
        
        InitCategoryButtons(vm.Categories, vm.SelectedCategoryIndex.Value);
        
        vm.SelectedCategoryIndex.Subscribe(index =>
        {
            UpdateSelectedButton(index);
        }).AddTo(this);
    }
    
    public void InitCategoryButtons(List<ItemCategory> categories, int defaultIndex = 0)
    {
        //TODO:优化
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
            if (config != null)
            {
                btn.Init(config.displayName, () => topVM.SetCategory(index));
            }
            else
            {
                btn.Init(categories[i].ToString(), () => topVM.SetCategory(index));
            }
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
        titleText.text = config != null ? config.displayName : selectedCategory.ToString();
    }
    
    
    #region 命令式操作（武器详情界面）
    public void SetTitle(string title)
    {
        if (titleText != null)
            titleText.text = title;
    }

    public void SetBgImage(bool show)
    {
        if (bgImage != null)
        {
            bgImage.gameObject.SetActive(show);
        }
    }

    /*public void SetIcon(Sprite sprite)
    {
        if (iconImage != null)
            iconImage.sprite = sprite;
    }

    public void SetBackButtonType(bool useArrow)
    {
        if (backArrowImage != null)
            backArrowImage.gameObject.SetActive(useArrow);
    }

    public void SetCoins(int coins)
    {
        if (coinText != null)
            coinText.text = coins.ToString();
        if (coinIcon != null)
            coinIcon.gameObject.SetActive(true);
        if (coinText != null)
            coinText.gameObject.SetActive(true);
    }

    public void HideCoins()
    {
        if (coinIcon != null) coinIcon.gameObject.SetActive(false);
        if (coinText != null) coinText.gameObject.SetActive(false);
    }*/

    public void HideCategoryButtons()
    {
        if (categoryBtnParent != null) categoryBtnParent.gameObject.SetActive(false);
        //if (categoryCountText != null) categoryCountText.gameObject.SetActive(false);
    }
    
    #endregion
}
