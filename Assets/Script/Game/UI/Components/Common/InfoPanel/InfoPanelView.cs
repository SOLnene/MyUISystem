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
   StarDisplay starDisplay;

   InfoPanelViewModel infoPanelVM;
   
    public void Bind(InfoPanelViewModel vm)
    {
        infoPanelVM = vm;
        vm.name.Subscribe(name => nameText.text = name).AddTo(this);
        vm.desc.Subscribe(desc => descText.text = desc).AddTo(this);
        vm.displayMainText.Subscribe(mainText => displayMainText.text = mainText).AddTo(this);
        vm.stars.Subscribe(stars=> starDisplay.SetStarLevel(stars,100,20)).AddTo(this);
        vm.color.Subscribe(color =>
        {
            //topBgImage.color = color;
            middleBgImage.color = color;
        }).AddTo(this);
        vm.iconPath.Where(path => !string.IsNullOrEmpty(path)).Subscribe(iconPath =>
        {
            LoadIconAsync(iconPath).Forget();
        }).AddTo(this);
    }

    async UniTask LoadIconAsync(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
        {
            return;
        }
        var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(iconPath);
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
        starDisplay.SetStarLevel(item.Stars,100,20);
    }
    
    private void UpdateDisplay(InventoryItem item)
    {
        nameText.text = item.ItemName;
        descText.text = item.Desc;
        displayMainText.text = item.GetDisplayMainText();
        Color color = RarityConfig.GetColor(item.ItemRarity);
        //topBgImage.color = color;
        middleBgImage.color = color;
        starDisplay.SetStarLevel(item.Stars,100,20);
    }
}
