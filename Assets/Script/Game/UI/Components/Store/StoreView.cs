using UnityEngine;

public class StoreView : UIView
{
    [SerializeField]
    StoreItemListView itemListView;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        itemListView.LoadFakeItems();
    }
}
