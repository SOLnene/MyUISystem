using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;


public partial class AchievementView : UIView
{
    [SerializeField]
    AchievementTopView topView;
    [SerializeField]
    AchievementCategoryTabListView categoryTabListView;
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
        viewModel = new AchievementViewModel(GameContext.Instance.AchievementService);
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
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        int itemCapacity = viewModel.Categories.Count == 0
            ? 0
            : viewModel.Categories.Max(category => category.Items.Count);
        if (!await achievementListView.PrepareAsync(itemCapacity, cancellationToken) ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        achievementListView.Refresh(viewModel.VisibleItems);
        // 当前分类切换、成就完成或领取后，VM 会重新计算右侧可见顺序。
        viewModel.VisibleItemsChanged
            .Subscribe(_ => achievementListView.Refresh(viewModel.VisibleItems))
            .AddTo(viewBindings);
        await categoryTabListView.BindAsync(
            viewModel.Categories,
            viewModel.SelectedCategoryId,
            viewModel.SelectCategory,
            cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await UniTask.WhenAll(
            categoryTabListView.ShowItems(),
            achievementListView.ShowItems());
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        viewModel.SelectedCategoryId
            .Skip(1)
            .Subscribe(_ => achievementListView.ShowItems().Forget())
            .AddTo(viewBindings);
    }

    void ReleaseViewState()
    {
        openCancellation?.Cancel();
        openCancellation?.Dispose();
        openCancellation = null;

        // View 关闭时只清理绑定和实例状态，动态 Item 本身由列表 View 复用。
        viewBindings.Clear();
        categoryTabListView.Clear();
        achievementListView.Clear();
        viewModel?.Dispose();
        viewModel = null;
    }
}
