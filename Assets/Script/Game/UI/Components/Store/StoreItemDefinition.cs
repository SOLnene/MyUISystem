using UnityEngine;

[CreateAssetMenu(menuName = "Game/Store/Store Item")]
public class StoreItemDefinition : ScriptableObject
{
    [SerializeField]
    string itemId;
    [SerializeField]
    int count = 1;
    [SerializeField]
    int price;
    [SerializeField, Range(0, 100)]
    int discountPercent;

    public string ItemId => itemId;
    public int Count => count;
    public int Price => price;
    public int DiscountPercent => discountPercent;
    public bool HasDiscount => discountPercent > 0;
}
