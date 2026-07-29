using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;


public partial class AchievementView : UIView
{
    [SerializeField]
    AchievementTopView topView;
    [SerializeField]
    AchievementListView achievementListView;

    AchievementViewModel viewModel;
    CancellationTokenSource openCancellation;
    readonly CompositeDisposable viewBindings = new();

    public override void OnOpen(object data)
    {
        base.OnOpen(data);

        openCancellation?.Cancel();
        openCancellation?.Dispose();
        viewBindings.Clear();
        openCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        viewModel?.Dispose();
        viewModel = new AchievementViewModel(GameDatabase.ItemDatabase);
        topView.Bind(viewModel.CountInfo, OnCancel);
        LoadItemsAsync(openCancellation.Token).Forget();
    }

    public override void OnClose()
    {
        ReleaseViewState();
        base.OnClose();
    }

    public override void OnRelease()
    {
        ReleaseViewState();
        base.OnRelease();
    }

    async UniTask LoadItemsAsync(CancellationToken cancellationToken)
    {
        await viewModel.LoadAsync(cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        viewModel.ItemOrderChanged
            .Subscribe(_ => achievementListView.Refresh(viewModel.Items))
            .AddTo(viewBindings);
        await achievementListView.BindAsync(viewModel.Items, cancellationToken);
    }

    void ReleaseViewState()
    {
        openCancellation?.Cancel();
        openCancellation?.Dispose();
        openCancellation = null;

        viewBindings.Clear();
        achievementListView.Clear();
        viewModel?.Dispose();
        viewModel = null;
    }
}
