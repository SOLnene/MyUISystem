using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailEquipPageView : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI nameText;
    [SerializeField]
    TextMeshProUGUI typeText;
    [SerializeField]
    TextMeshProUGUI firstStatValueText;
    [SerializeField]
    TextMeshProUGUI secondStatValueText;
    [SerializeField]
    TextMeshProUGUI levelText;
    [SerializeField]
    TextMeshProUGUI levelCapText;
    [SerializeField]
    GameObject[] rareStars;
    [SerializeField]
    Image[] promoteStars;
    [SerializeField]
    TextMeshProUGUI refineLevelText;
    [SerializeField]
    TextMeshProUGUI descText;
    [SerializeField]
    Button replaceButton;
    [SerializeField]
    Button enhanceButton;
    [SerializeField]
    ItemSelectPanelView itemSelectPopupViewPrefab;

    CharacterDetailEquipPageViewModel vm;
    CompositeDisposable disposable = new CompositeDisposable();
    readonly CompositeDisposable weaponDisposable = new CompositeDisposable();
    
    public void Bind(CharacterDetailEquipPageViewModel viewModel)
    {
        disposable.Clear();
        weaponDisposable.Clear();
        vm = viewModel;
        if (viewModel == null)
        {
            return;
        }

        viewModel.currentWeapon
            .Subscribe(BindWeapon)
            .AddTo(disposable);

        if (replaceButton != null)
        {
            replaceButton.onClick.RemoveAllListeners();
            replaceButton.onClick.AddListener(OnReplaceButtonClicked);
            vm.replaceButtonInteractable
                .Subscribe(interactable => replaceButton.interactable = interactable)
                .AddTo(disposable);
        }

        if (enhanceButton != null)
        {
            enhanceButton.onClick.RemoveAllListeners();
            enhanceButton.onClick.AddListener(OnEnhanceButtonClick);
        }
        
        if(itemSelectPopupViewPrefab != null)
        {
            itemSelectPopupViewPrefab.gameObject.SetActive(false);
        }
    }

    void BindWeapon(EquipItemViewModel weapon)
    {
        weaponDisposable.Clear();

        if (weapon == null)
        {
            return;
        }

        RefreshView();

        weapon.Changed
            .Subscribe(_ => RefreshView())
            .AddTo(weaponDisposable);
    }

    public void RefreshView()
    {
        var weapon = vm?.currentWeapon.Value;
        if (weapon == null)
        {
            return;
        }

        if (nameText != null) nameText.text = weapon.name.Value;
        //if (typeText != null) typeText.text = weapon.Model.Category.ToString();
        if (firstStatValueText != null) firstStatValueText.text = weapon.Model.GetDisplayMainStatText();
        if (secondStatValueText != null) secondStatValueText.text = weapon.Model.GetDisplaySubStatText();
        if (levelText != null) levelText.text = $"Lv.{weapon.level.Value}";
        if (levelCapText != null) levelCapText.text = $"/ {weapon.Model.GetMaxLevel()}";
        if (refineLevelText != null) refineLevelText.text = $"精炼{weapon.refineLevel.Value}阶";
        if (descText != null) descText.text = weapon.desc.Value;

        SetStars(rareStars, weapon.star.Value);
        SetPromoteStars(weapon.rank.Value);
    }

    void SetStars(GameObject[] stars, int count)
    {
        if (stars == null)
        {
            return;
        }

        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] != null)
            {
                stars[i].SetActive(i < count);
            }
        }
    }

    void SetPromoteStars(int count)
    {
        if (promoteStars == null)
        {
            return;
        }

        for (int i = 0; i < promoteStars.Length; i++)
        {
            if (promoteStars[i] != null)
            {
                promoteStars[i].color = i < count ? Color.white : Color.gray;
            }
        }
    }
    
    public void OpenChangeWeaponPanel()
    {
        vm.BeginSelectWeapon();
        var param = new SinglePickParams(
            new ItemFilter(ItemCategory.Equip, 5),
            item =>
            {
                if (item is EquipItem equipItem)
                {
                    vm.SelectWeapon(equipItem);
                }
            },
            false,vm.CancelSelect);
        itemSelectPopupViewPrefab.Show(param);
    }

    void OnReplaceButtonClicked()
    {
        if (vm.HasPreviewWeapon())
        {
            vm.ConfirmChangeWeapon();
        }
        else
        {
            OpenChangeWeaponPanel();
        }
    }

    void OpenSelectPanel()
    {
        itemSelectPopupViewPrefab.gameObject.SetActive(true);
    }
    
    void OnEnhanceButtonClick()
    {
        var weapon = vm.currentWeapon.Value;
        if (weapon == null)
        {
            return;
        }
        
        UIManager.Instance.Open(UIType.EquipDetailView,new EquipDetailOpenParams(weapon,WeaponDetailTab.Enhance));
    }
    
    void OnDestroy()
    {
        weaponDisposable.Dispose();
        disposable.Dispose();
    }
}
