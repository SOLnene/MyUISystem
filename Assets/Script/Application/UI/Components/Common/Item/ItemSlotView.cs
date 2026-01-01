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

public class ItemSlotView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
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

    
    public ItemSlotViewModel vm { get; private set; }
    
    bool isSelected = false;
    
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
            foreach (Transform child in starParent)
            {
                Destroy(child.gameObject);
            }
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
            if (selected)
            {
                PlayBreathingEffect();
            }
            else
            {
                StopBreathingEffect();
            }
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
        Debug.Log($"[LoadIconAsync] 开始加载 {iconPath}, ver={requestVersion}");

        var sprite = await ResourceManager.Instance.LoadAssetAsync<Sprite>(iconPath);

        if (sprite == null)
        {
            Debug.LogError($"[LoadIconAsync] 加载失败: {iconPath}");
            return;
        }

        Debug.Log($"[LoadIconAsync] 加载成功: {sprite.name} ({sprite.texture?.name})");
    
        // 检查版本、状态是否被回收
        if (this == null || vm == null || requestVersion != iconRequestVersion || vm.iconPath.Value != iconPath)
        {
            Debug.LogWarning($"[LoadIconAsync] 加载完成但已失效，跳过。ver={requestVersion}, 当前={iconRequestVersion}");
            return;
        }

        icon.sprite = sprite;
        Debug.Log($"[LoadIconAsync] 已赋值给 Image: {icon.sprite?.name}");
    }
    
    public void SetStarLevel(int level)
    {
        string path = "Assets/Sprite/Backpack/UI_IconStar.png";
        ResourceManager.Instance.LoadAssetAsync<Sprite>(path, sprite =>
        {
            if (sprite == null)
            {
                Debug.LogError($"加载星级图片失败: {path}");
                return;
            }
            for (int i = 0; i < level; i++)
            {
                CreateStarImage(sprite);
            }
        });
    }
    
    private void CreateStarImage(Sprite sprite)
    {
        GameObject starGO = new GameObject("StarIcon", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = starGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(40, 40);  // 宽32, 高32 像素
        var image = starGO.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = new Color(1.0f, 0.8f, 0.2f);
        //image.SetNativeSize();

        // 放到目标容器下，例如 Slot/Stars
        starGO.transform.SetParent(starParent, false);
    }
    #region 动画相关
    public void OnPointerEnter(PointerEventData eventData)
    {
        
        if (!isSelected) // 选中状态交给 ViewModel 管理，Hover 不覆盖选中动画
        {
            PlayHoverAnimation();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isSelected) // 如果是选中状态，退出 hover 时保持选中动画
        {
            PlayUnhoverAnimation();
        }
    }

    private void PlayHoverAnimation()
    {
        hoverTween?.Kill();
        //防止透明度为0的bug
        hoverTween = DOTween.Sequence()
            .Append(transform.DOScale(1.1f, 0.15f).SetEase(Ease.OutBack))
            .Join(glowEffectImage.DOFade(1.0f,0.15f))
            .SetUpdate(true);

    }

    private void PlayUnhoverAnimation()
    {
        hoverTween?.Kill();
        hoverTween = DOTween.Sequence()
            .Append(transform.DOScale(1.0f, 0.15f).SetEase(Ease.OutBack))
            .Join(glowEffectImage.DOFade(0.0f,0.15f))
            .SetUpdate(true);
    }
    


    private void PlayBreathingEffect()
    {
        hoverTween?.Kill();
        loopTween?.Kill();

        loopTween = DOTween.Sequence()
            // 同时放大和光圈亮度变化
            .Append(glowEffectImage.transform.DOScale(1.1f, 1.0f).From(1.0f).SetEase(Ease.InOutSine))
            .Join(glowEffectImage.DOFade(1.0f, 1.0f).From(0f).SetEase(Ease.InOutSine)) // 从暗到亮
            .Append(glowEffectImage.transform.DOScale(1.0f, 1.0f).From(1.1f).SetEase(Ease.InOutSine))
            .Join(glowEffectImage.DOFade(0.0f, 1.0f).SetEase(Ease.InOutSine)) // 最小时最暗
            .SetLoops(-1, LoopType.Yoyo)  // 无限循环
            .SetUpdate(true);
    }
    
    private void StopBreathingEffect()
    {
        loopTween?.Kill();
        glowEffectImage.transform.localScale = Vector3.zero;
        glowEffectImage.color = new Color(glowEffectImage.color.r, glowEffectImage.color.g, glowEffectImage.color.b, 0f);
    }
      #endregion
}
