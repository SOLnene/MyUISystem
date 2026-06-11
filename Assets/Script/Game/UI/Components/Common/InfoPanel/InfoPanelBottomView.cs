using TMPro;
using UniRx;
using UnityEngine;

public class InfoPanelBottomView : MonoBehaviour
{
    [SerializeField]
    GameObject upgradeStatusContent;
    [SerializeField]
    GameObject effectArea;
    [SerializeField]
    LevelCapLineView levelCapLine;
    [SerializeField]
    PromoteStarsView promoteStarsView;
    [SerializeField]
    TextMeshProUGUI refineLevelValueText;
    [SerializeField]
    TextMeshProUGUI refineLevelDescriptionText;
    [SerializeField]
    TextMeshProUGUI effectText;
    [SerializeField]
    TextMeshProUGUI descText;

    readonly CompositeDisposable viewModelDisposables = new();
    readonly CompositeDisposable itemDisposables = new();

    public void Bind(InfoPanelViewModel viewModel)
    {
        viewModelDisposables.Clear();
        itemDisposables.Clear();

        viewModel.currentItem
            .Where(item => item != null)
            .Subscribe(BindItem)
            .AddTo(viewModelDisposables);
    }

    public void Display(InventoryItem item)
    {
        itemDisposables.Clear();
        descText.text = item.Desc;
        ApplyItemType(item);

        if (item is EquipItem equipItem)
        {
            ApplyWeaponStatus(equipItem);
        }
    }

    void BindItem(ItemViewModel itemViewModel)
    {
        itemDisposables.Clear();
        itemViewModel.desc
            .Subscribe(desc => descText.text = desc)
            .AddTo(itemDisposables);

        InventoryItem item = itemViewModel.Model;
        ApplyItemType(item);
        if (item is not EquipItem equipItem)
        {
            return;
        }

        ApplyWeaponStatus(equipItem);
        if (itemViewModel is EquipItemViewModel equipViewModel)
        {
            equipViewModel.level
                .Subscribe(_ => ApplyWeaponStatus(equipViewModel.Model))
                .AddTo(itemDisposables);
            equipViewModel.rank
                .Subscribe(_ => ApplyWeaponStatus(equipViewModel.Model))
                .AddTo(itemDisposables);
            equipViewModel.refineLevel
                .Subscribe(_ => ApplyWeaponStatus(equipViewModel.Model))
                .AddTo(itemDisposables);
        }
    }

    void ApplyItemType(InventoryItem item)
    {
        bool isWeapon = item is EquipItem;
        bool showEffect = isWeapon || item.Category == ItemCategory.Consumable;

        upgradeStatusContent.SetActive(isWeapon);
        effectArea.SetActive(showEffect);
        effectText.text = "暂无效果";
    }

    void ApplyWeaponStatus(EquipItem equipItem)
    {
        levelCapLine.SetValue(equipItem.Level, equipItem.GetCurrentMaxLevel());
        promoteStarsView.SetRank(equipItem.Rank);
        refineLevelValueText.text = equipItem.RefinementLevel.ToString();
        refineLevelDescriptionText.text = $"精炼{equipItem.RefinementLevel}阶";
    }

    void OnDestroy()
    {
        viewModelDisposables.Dispose();
        itemDisposables.Dispose();
    }
}
