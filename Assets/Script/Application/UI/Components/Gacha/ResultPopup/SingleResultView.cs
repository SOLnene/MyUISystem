using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SkierFramework;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class SingleResultView : MonoBehaviour,IBindableUI
{
    SingleResultViewModel vm;
    CompositeDisposable disposable = new CompositeDisposable();
    
    #region 控件绑定变量声明，自动生成请勿手改
		#pragma warning disable 0649
    [ControlBinding]
    private Image icon;
    [ControlBinding]
    private TextMeshProUGUI nameText;

		#pragma warning restore 0649
#endregion
    
    public void Bind(SingleResultViewModel viewModel)
    {
        BindData();
        disposable.Clear();
        viewModel.item.Subscribe(
             item =>
            {
                UpdateView(item);
            }).AddTo(disposable);
    }

    public void BindData()
    {
        UIControlData uiControlData = GetComponent<UIControlData>();
        if (uiControlData != null)
        {
            uiControlData.BindDataTo(this);
        }
    }

    void UpdateView(GachaEquipItemViewModel equip)
    {
        // 清除旧订阅
        disposable.Clear();

        if (equip == null)
        {
            icon.sprite = null;
            nameText.text = string.Empty;
            return;
        }

        // 强制立即同步一次
        nameText.text = equip.Name.Value;
        icon.sprite = equip.Icon.Value;

        // 订阅 VM → UI
        equip.Name.Subscribe(v => nameText.text = v)
            .AddTo(disposable);

        equip.Icon.Subscribe(v => icon.sprite = v)
            .AddTo(disposable);
        
        
    }

    
    async UniTask UpdateViewAsync(GachaEquipItemViewModel equip)
    {
        if (equip == null)
        {
            icon.sprite = null;
            nameText.text = string.Empty;
            return;
        }

        nameText.text = equip.Name.Value;
        icon.sprite = equip.Icon.Value;
    }
    
    async UniTask<Sprite> GetGachaIconAsync(string equipName)
    {
        if (string.IsNullOrEmpty(equipName))
            return null;

        string path = $"ui_gacha_equipicon_{equipName}";
        try
        {
            return await ResourceManager.Instance.LoadAssetAsync<Sprite>(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SingleResultView] 加载图标失败：{path} - {e.Message}");
            return null;
        }
    }
    
}
