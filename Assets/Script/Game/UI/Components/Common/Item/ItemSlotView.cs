using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using DG.Tweening;
using UnityEngine.EventSystems;

public class ItemSlotView : UIThreeStateSelectable
{
    [Header("Tmpro Text")]
    [SerializeField]
    TextMeshProUGUI itemNameText;
    [SerializeField]
    TextMeshProUGUI itemCountText;
    
    [Space]
    [Header("图片")]
    [SerializeField]
    Image bgImage;
    [SerializeField]
    Image glowEffectImage;
    [SerializeField]
    Image colorImage;
    [SerializeField]
    Image icon;
    [SerializeField]
    Image checkedImage;
    
    [Space]
    [Header("按钮")]
    [SerializeField]
    Button itemBtn;
    [SerializeField]
    Button removeBtn;
    
    [Space]
    [SerializeField]
    Transform starParent;

    
    [Space]
    [Header("经验书")]
    [SerializeField]
    GameObject expbookArea;
    [SerializeField]
    TextMeshProUGUI selectedValueText;
    
    public ItemSlotViewModel vm { get; private set; }
    
    //不知道为什么不用vm的select用这个
    
    Tween selectTween;
    Tween hoverTween;
    Tween loopTween;

    // 本地请求版本号，用来判定异步结果是否仍然有效
    int iconRequestVersion = 0;
    
    CompositeDisposable disposable = new CompositeDisposable();
    
    //todo:只穿绑定方法，使格子可以复用
    public void Bind(ItemSlotViewModel vm)
    {
        this.vm = vm;

        if (vm == null)
        {
            ClearView();
            return;
        }

        // 初始化状态
        itemCountText.gameObject.SetActive(!vm.isEmpty.Value);
        checkedImage.gameObject.SetActive(vm.isChecked.Value);
        removeBtn.gameObject.SetActive(vm.isChecked.Value);
        icon.sprite = null;
        
        disposable.Clear();
        SetSelected(vm.isSelected.Value, true);

        if (vm.isEmpty.Value)
        {
            Debug.Log($"empty");
            LoadIconAsync("Assets/AssetsPackage/UI/Sprite/TouchIcon/UI_TouchIcon_Plus.png", ++iconRequestVersion)
                .AttachExternalCancellation(this.GetCancellationTokenOnDestroy())
                .Forget();
        }
        
        vm.isEmpty.Subscribe(empty =>
        {
            itemCountText.gameObject.SetActive(!empty);
            if (empty)
            {
                //ClearView();
            }
        }).AddTo(disposable);
        
        vm.count.Where(count=>count!=null).Subscribe(count =>
        {
            itemCountText.text = count;
        }).AddTo(disposable);

        vm.color.Subscribe(color => colorImage.color = color).AddTo(this);
        
        vm.star.Subscribe(star =>
        {
            SetStarLevel(star);
        }).AddTo(disposable);
        
        vm.iconPath.Where(path=>!string.IsNullOrEmpty(path))
            .Subscribe(path =>
            {
                var reqVeison = ++iconRequestVersion;
                    LoadIconAsync(path,reqVeison)
                    .AttachExternalCancellation(this.GetCancellationTokenOnDestroy())
                    .Forget();
            })
            .AddTo(disposable);
        
        vm.isSelected.Subscribe(selected =>
        {
            SetSelected(selected);
        }).AddTo(disposable);
        
        vm.isChecked.Subscribe(selected =>
        {
            checkedImage.gameObject.SetActive(selected);
            removeBtn.gameObject.SetActive(selected);
        }).AddTo(disposable);

        
        
        itemBtn.onClick.RemoveAllListeners();
        itemBtn.onClick.AddListener(() =>
        {
            vm.onClick.Execute();
        });
        
        removeBtn.onClick.RemoveAllListeners();
        removeBtn.onClick.AddListener(() =>
        {
            vm.onRemove.Execute();
        });
        
        //绑定经验书部分
        vm.selectedCount.Subscribe(
            value =>
            {
                expbookArea.SetActive(value > 0);
                if (value > 0)
                {
                    selectedValueText.text = value.ToString();
                }
            });
    }
    private void ClearView()
    {
        itemNameText.text = "";
        itemCountText.text = "";
        itemCountText.gameObject.SetActive(false);
        icon.sprite = null;
        checkedImage.gameObject.SetActive(false);
        removeBtn.gameObject.SetActive(false);

        foreach (Transform child in starParent)
            Destroy(child.gameObject);
    }
    
    
    async UniTask LoadIconAsync(string iconPath,int requestVersion)
    {
        //Debug.Log($"[LoadIconAsync] 开始加载 {iconPath}, ver={requestVersion}");

        var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(iconPath);

        if (sprite == null)
        {
            Debug.LogError($"[LoadIconAsync] 加载失败: {iconPath}");
            return;
        }

        //Debug.Log($"[LoadIconAsync] 加载成功: {sprite.name} ({sprite.texture?.name})");
    
        // 检查版本、状态是否被回收
        if (this == null || vm == null || requestVersion != iconRequestVersion || vm.iconPath.Value != iconPath)
        {
            Debug.LogWarning($"[LoadIconAsync] 加载完成但已失效，跳过。ver={requestVersion}, 当前={iconRequestVersion}");
            return;
        }

        icon.sprite = sprite;
        //Debug.Log($"[LoadIconAsync] 已赋值给 Image: {icon.sprite?.name}");
    }

    public void SetStarLevel(int level)
    {
        int childCount = starParent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            starParent.GetChild(i).gameObject.SetActive(i < level);
        }
    }

   
    #region 动画相关
    protected override void ApplyVisualState(VisualState state, bool instant, bool stateChanged)
    {
        hoverTween?.Kill();
        loopTween?.Kill();

        switch (state)
        {
            case VisualState.Normal:
                ApplyNormalState(instant || !stateChanged);
                break;
            case VisualState.Hover:
                ApplyHoverState(instant || !stateChanged);
                break;
            case VisualState.Selected:
                ApplySelectedState(instant || !stateChanged);
                break;
        }
    }

    void ApplyNormalState(bool instant)
    {
        SetGlowScale(Vector3.zero);
        if (instant)
        {
            transform.localScale = Vector3.one;
            SetGlowAlpha(0f);
            return;
        }

        hoverTween = DOTween.Sequence()
            .Append(transform.DOScale(1.0f, 0.15f).SetEase(Ease.OutBack))
            .Join(glowEffectImage.DOFade(0.0f, 0.15f))
            .SetUpdate(true);
    }

    void ApplyHoverState(bool instant)
    {
        SetGlowScale(Vector3.one);
        if (instant)
        {
            transform.localScale = Vector3.one * 1.1f;
            SetGlowAlpha(1f);
            return;
        }

        hoverTween = DOTween.Sequence()
            .Append(transform.DOScale(1.1f, 0.15f).SetEase(Ease.OutBack))
            .Join(glowEffectImage.DOFade(1.0f, 0.15f))
            .SetUpdate(true);
    }

    void ApplySelectedState(bool instant)
    {
        transform.localScale = Vector3.one;
        SetGlowScale(Vector3.one);
        if (instant)
        {
            SetGlowAlpha(1f);
        }
        
        loopTween = DOTween.Sequence()
            .Append(glowEffectImage.transform.DOScale(1.1f, 1.0f).From(1.0f).SetEase(Ease.InOutSine))
            .Join(glowEffectImage.DOFade(1.0f, 1.0f).From(0f).SetEase(Ease.InOutSine))
            .Append(glowEffectImage.transform.DOScale(1.0f, 1.0f).From(1.1f).SetEase(Ease.InOutSine))
            .Join(glowEffectImage.DOFade(0.0f, 1.0f).SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }
    
    void SetGlowScale(Vector3 scale)
    {
        glowEffectImage.transform.localScale = scale;
    }

    void SetGlowAlpha(float alpha)
    {
        glowEffectImage.color = new Color(glowEffectImage.color.r, glowEffectImage.color.g, glowEffectImage.color.b, alpha);
    }
      #endregion
}
