using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;


public partial class AchievementView : UIView
{
    [SerializeField]
    AchievementTopView topView;
    [SerializeField]
    AchievementListView achievementListView;

    AchievementViewModel viewModel;
    CancellationTokenSource openCancellation;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);

        openCancellation?.Cancel();
        openCancellation?.Dispose();
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

        await achievementListView.BindAsync(viewModel.Items, cancellationToken);
    }

    void ReleaseViewState()
    {
        openCancellation?.Cancel();
        openCancellation?.Dispose();
        openCancellation = null;

        achievementListView.Clear();
        viewModel?.Dispose();
        viewModel = null;
    }
}
