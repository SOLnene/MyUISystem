using UniRx;
using UnityEngine;

public class StoreView : UIView
{
    [SerializeField]
    StoreTopView topView;
    [SerializeField]
    StoreTabView tabView;
    [SerializeField]
    StoreItemListView itemListView;

    StoreViewModel viewModel;
    readonly CompositeDisposable disposable = new();

    public override void OnOpen(object data)
    {
        base.OnOpen(data);
        disposable.Clear();
        topView.Bind(OnCancel);
        viewModel = new StoreViewModel(GameContext.Instance.StoreDatabase, GameDatabase.ItemDatabase);
        tabView.Bind(viewModel);
        viewModel.CurrentTab
            .Subscribe(tab => itemListView.Bind(viewModel.CreateItems(tab)))
            .AddTo(disposable);
    }

    public override void OnClose()
    {
        disposable.Clear();
        base.OnClose();
    }
}
