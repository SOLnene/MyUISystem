using Cysharp.Threading.Tasks;
using UnityEngine;

public class BackpackView : UIView
{
    [SerializeField]
    BackpackTopView topView;
    [SerializeField]
    BackpackMiddleView middleView;
    [SerializeField]
    BottomView bottomHub;
    [SerializeField]
    GameObject inputBlock;

    BackpackViewModel vm;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);

        vm = data as BackpackViewModel ?? GameContext.Instance.BackpackVM;
        Bind(vm);
        ShowHubs().Forget();
    }

    public override void OnClose()
    {
        topView.OnBackClicked -= CloseSelf;
        base.OnClose();
    }

    void Bind(BackpackViewModel viewModel)
    {
        topView.OnBackClicked -= CloseSelf;
        topView.OnBackClicked += CloseSelf;

        // ReactiveProperty 对相同值会去重，已是 0 时需主动刷新分类以重新选中首个物品。
        if (viewModel.topVM.SelectedCategoryIndex.Value != 0)
        {
            viewModel.topVM.SetCategory(0);
        }
        else
        {
            viewModel.middleVM.FilterByCategory(viewModel.topVM.CurrentCategory);
        }

        topView.Bind(viewModel.topVM);
        middleView.Bind(viewModel.middleVM, viewModel.infoVM);

    }

    void CloseSelf()
    {
        OnCancel();
    }

    public override void OnCancel()
    {
        if (inputBlock.activeSelf)
        {
            return;
        }

        CloseWithAnimation().Forget();
    }

    async UniTask ShowHubs()
    {
        inputBlock.SetActive(true);
        await UniTask.WhenAll(topView.Show(), middleView.Show(), bottomHub.Show());
        inputBlock.SetActive(false);
    }

    async UniTask CloseWithAnimation()
    {
        inputBlock.SetActive(true);
        await UniTask.WhenAll(topView.Hide(), middleView.Hide(), bottomHub.Hide());
        base.OnCancel();
    }
}
