using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Debug = System.Diagnostics.Debug;

public class InfoPanelView : MonoBehaviour
{
    [Header("文本")]
    [SerializeField]
    TextMeshProUGUI nameText;
    [SerializeField]
    TextMeshProUGUI categoryValue;
    [SerializeField]
    InfoPanelBottomView bottomView;
    [SerializeField]
    GameObject statArea;
    [SerializeField]
    TextMeshProUGUI mainStatLabelText;
    [SerializeField]
    TextMeshProUGUI mainStatValueText;
    [SerializeField]
    TextMeshProUGUI subStatLabelText;
    [SerializeField]
    TextMeshProUGUI subStatValueText;
    
    
    [Header("背景图")]
    [SerializeField]
    Image topBgImage;
    [SerializeField]
    Image middleBgImage;
   // BackpackMiddleViewModel vm;
   [Header("Icon")]
   [SerializeField]
   Image icon;
   
   [SerializeField]
   GameObject[] starIcons;

   [SerializeField]
   AnimatedPanel animatedPanel;
   
   InfoPanelViewModel infoPanelVM;

   CompositeDisposable disposable = new();
   CancellationTokenSource iconLoadCts;
   int iconRequestVersion;
    public void Bind(InfoPanelViewModel vm)
    {
        disposable.Clear();
        if (vm == null)
        {
            CancelIconLoad();
            icon.sprite = null;
            return;
        }

        infoPanelVM = vm;
        vm.name.Subscribe(name => nameText.text = name).AddTo(disposable);
        vm.categoryValue.Subscribe(category => categoryValue.text = category).AddTo(disposable);
        bottomView.Bind(vm);
        vm.showStatArea.Subscribe(show => statArea.SetActive(show)).AddTo(disposable);
        vm.mainStatLabel.Subscribe(label => mainStatLabelText.text = label).AddTo(disposable);
        vm.mainStatValue.Subscribe(value => mainStatValueText.text = value).AddTo(disposable);
        vm.subStatLabel.Subscribe(label => subStatLabelText.text = label).AddTo(disposable);
        vm.subStatValue.Subscribe(value => subStatValueText.text = value).AddTo(disposable);
        vm.stars.Subscribe(SetStars).AddTo(disposable);
        vm.color.Subscribe(color =>
        {
            //topBgImage.color = color;
            middleBgImage.color = color;
        }).AddTo(disposable);
        vm.iconPath.Subscribe(SetIcon).AddTo(disposable);
    }

    public void Show(ItemViewModel itemViewModel)
    {
        if (itemViewModel == null)
        {
            return;
        }

        if (infoPanelVM == null)
        {
            Bind(new InfoPanelViewModel());
        }

        infoPanelVM.Bind(itemViewModel);
        gameObject.SetActive(true);

        if (animatedPanel != null)
        {
            animatedPanel.Show().Forget();
        }
    }

    public void Hide()
    {
        HideAsync().Forget();
    }

    async UniTask HideAsync()
    {
        if (animatedPanel != null)
        {
            await animatedPanel.Hide();
            return;
        }

        gameObject.SetActive(false);
    }

    void SetIcon(string iconPath)
    {
        CancelIconLoad();
        icon.sprite = null;

        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        int requestVersion = iconRequestVersion;
        var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        iconLoadCts = requestCts;
        LoadIconAsync(iconPath, requestVersion, requestCts).Forget();
    }

    async UniTask LoadIconAsync(
        string iconPath,
        int requestVersion,
        CancellationTokenSource requestCts)
    {
        try
        {
            var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(
                iconPath,
                requestCts.Token);
            if (requestVersion != iconRequestVersion ||
                !ReferenceEquals(iconLoadCts, requestCts))
            {
                return;
            }

            icon.sprite = sprite;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(iconLoadCts, requestCts))
            {
                iconLoadCts = null;
                requestCts.Dispose();
            }
        }
    }

    void CancelIconLoad()
    {
        iconRequestVersion++;
        var requestCts = iconLoadCts;
        iconLoadCts = null;
        requestCts?.Cancel();
        requestCts?.Dispose();
    }
    
    /// <summary>
    /// 预留外部调用接口，可能没用
    /// </summary>
    /// <param name="item"></param>
    public void Refresh(InventoryItem item)
    {
        nameText.text = item.ItemName;
        categoryValue.text = ItemCategoryDisplayName.Get(item.Category);
        bottomView.Display(item);
        ApplyStatArea(item);
        Color color = RarityConfig.GetColor(item.ItemRarity);
        //topBgImage.color = color;
        middleBgImage.color = color;
        SetStars(item.Stars);
    }
    
    private void UpdateDisplay(InventoryItem item)
    {
        nameText.text = item.ItemName;
        categoryValue.text = ItemCategoryDisplayName.Get(item.Category);
        bottomView.Display(item);
        ApplyStatArea(item);
        Color color = RarityConfig.GetColor(item.ItemRarity);
        //topBgImage.color = color;
        middleBgImage.color = color;
        SetStars(item.Stars);
    }

    void ApplyStatArea(InventoryItem item)
    {
        bool showStats = item is EquipItem;
        statArea.SetActive(showStats);
        if (!showStats)
        {
            return;
        }

        var equipItem = (EquipItem)item;
        mainStatLabelText.text = "基础攻击力";
        mainStatValueText.text = equipItem.GetDisplayMainStatText();
        subStatLabelText.text = "暴击伤害";
        subStatValueText.text = equipItem.GetCriticalDamage().ToString("P1");
    }

    void SetStars(int stars)
    {
        if (starIcons == null)
        {
            return;
        }

        for (int i = 0; i < starIcons.Length; i++)
        {
            if (starIcons[i] != null)
            {
                starIcons[i].SetActive(i < stars);
            }
        }
    }

    void OnDestroy()
    {
        CancelIconLoad();
        disposable.Dispose();
    }

    void OnDisable()
    {
        CancelIconLoad();
    }
}
