using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;

public class ItemSlotView : MonoBehaviour
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
    Image checkedImage;
    [SerializeField]
    RectTransform newItemRedDot;
    [SerializeField]
    AnimatedPanel anim;
    [SerializeField]
    SelectionSlotView selectionSlot;
    
    [Space]
    [Header("按钮")]
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
    
    CompositeDisposable disposable = new CompositeDisposable();
    
    //todo:只穿绑定方法，使格子可以复用
    public void Bind(ItemSlotViewModel vm)
    {
        ResetState();
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
        
        disposable.Clear();
        selectionSlot.SetSelected(vm.isSelected.Value, true);

        if (vm.isEmpty.Value)
        {
            Debug.Log($"empty");
            selectionSlot.LoadIcon("Assets/AssetsPackage/UI/Sprite/TouchIcon/UI_TouchIcon_Plus.png");
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

        vm.color.Subscribe(color => selectionSlot.SetRarityColor(color)).AddTo(disposable);
        
        vm.star.Subscribe(star =>
        {
            SetStarLevel(star);
        }).AddTo(disposable);
        
        vm.iconPath.Where(path=>!string.IsNullOrEmpty(path))
            .Subscribe(path =>
            {
                selectionSlot.LoadIcon(path);
            })
            .AddTo(disposable);
        
        vm.isSelected.Subscribe(selected =>
        {
            selectionSlot.SetSelected(selected);
        }).AddTo(disposable);
        
        vm.isChecked.Subscribe(selected =>
        {
            checkedImage.gameObject.SetActive(selected);
            removeBtn.gameObject.SetActive(selected);
        }).AddTo(disposable);

        vm.isNew.Subscribe(value => newItemRedDot.gameObject.SetActive(value)).AddTo(disposable);

        
        
        selectionSlot.SetClickListener(() => vm.onClick.Execute());
        
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
            }).AddTo(disposable);
    }

    public UniTask Show(bool instant = false)
    {
        return anim.Show(instant);
    }

    public UniTask<bool> Hide(bool instant = false)
    {
        return anim.Hide(instant);
    }

    public void HideImmediate()
    {
        anim.HideImmediate();
    }

    public void ResetState()
    {
        selectionSlot.ResetState();
        disposable.Clear();
        removeBtn.onClick.RemoveAllListeners();
        newItemRedDot.gameObject.SetActive(false);
        vm = null;
    }

    private void ClearView()
    {
        itemNameText.text = "";
        itemCountText.text = "";
        itemCountText.gameObject.SetActive(false);
        selectionSlot.ClearIcon();
        checkedImage.gameObject.SetActive(false);
        removeBtn.gameObject.SetActive(false);

        foreach (Transform child in starParent)
            Destroy(child.gameObject);
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
    void OnDestroy()
    {
        ResetState();
        disposable.Dispose();
    }
      #endregion
}
