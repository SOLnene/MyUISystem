using UnityEngine;

[CreateAssetMenu(menuName = "Game/Store/Store Item")]
public class StoreItemDefinition : ScriptableObject
{
    [SerializeField]
    int storeItemId;
    [SerializeField]
    int itemId;
    [SerializeField]
    int count = 1;
    [SerializeField]
    int costItemId;
    [SerializeField]
    int price;
    [SerializeField, Range(0, 100)]
    int discountPercent;

    public int StoreItemId => storeItemId;
    public int ItemId => itemId;
    public int Count => count;
    public int CostItemId => costItemId;
    public int Price => price;
    public int DiscountPercent => discountPercent;
    public bool HasDiscount => discountPercent > 0;
}
