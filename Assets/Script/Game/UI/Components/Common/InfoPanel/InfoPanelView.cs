using System.Collections;
using System.Collections.Generic;
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
    TextMeshProUGUI descText;
    [SerializeField]
    TextMeshProUGUI displayMainText;
    
    
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

   InfoPanelViewModel infoPanelVM;

   CompositeDisposable disposable = new();
   string currentIconPath;
    public void Bind(InfoPanelViewModel vm)
    {
        disposable.Clear();
        if (vm == null)
        {
            return;
        }

        infoPanelVM = vm;
        vm.name.Subscribe(name => nameText.text = name).AddTo(disposable);
        vm.desc.Subscribe(desc => descText.text = desc).AddTo(disposable);
        vm.displayMainText.Subscribe(mainText => displayMainText.text = mainText).AddTo(disposable);
        vm.stars.Subscribe(SetStars).AddTo(disposable);
        vm.color.Subscribe(color =>
        {
            //topBgImage.color = color;
            middleBgImage.color = color;
        }).AddTo(disposable);
        vm.iconPath.Where(path => !string.IsNullOrEmpty(path)).Subscribe(iconPath =>
        {
            currentIconPath = iconPath;
            LoadIconAsync(iconPath).Forget();
        }).AddTo(disposable);
    }

    async UniTask LoadIconAsync(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }
        var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(iconPath);
        if (currentIconPath != iconPath)
        {
            return;
        }

        icon.sprite = sprite;
    }
    
    /// <summary>
    /// 预留外部调用接口，可能没用
    /// </summary>
    /// <param name="item"></param>
    public void Refresh(InventoryItem item)
    {
        nameText.text = item.ItemName;
        descText.text = item.Desc;
        displayMainText.text = item.GetDisplayMainText();
        Color color = RarityConfig.GetColor(item.ItemRarity);
        //topBgImage.color = color;
        middleBgImage.color = color;
        SetStars(item.Stars);
    }
    
    private void UpdateDisplay(InventoryItem item)
    {
        nameText.text = item.ItemName;
        descText.text = item.Desc;
        displayMainText.text = item.GetDisplayMainText();
        Color color = RarityConfig.GetColor(item.ItemRarity);
        //topBgImage.color = color;
        middleBgImage.color = color;
        SetStars(item.Stars);
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
        disposable.Dispose();
    }
}
