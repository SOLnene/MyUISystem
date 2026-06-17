using UnityEngine;

public class StoreView : UIView
{
    [SerializeField]
    StoreTopView topView;
    [SerializeField]
    StoreItemListView itemListView;

    StoreViewModel viewModel;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        topView.Bind(OnCancel);
        viewModel = new StoreViewModel(GameContext.Instance.StoreDatabase, GameDatabase.ItemDatabase);
        itemListView.Bind(viewModel.CreateItems());
    }
}
