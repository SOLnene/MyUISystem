using UnityEngine;

public class BackpackView : UIView
{
    [SerializeField]
    BackpackTopView topView;
    [SerializeField]
    BackpackMiddleView middleView;
    [SerializeField]
    GameObject bottomHub;

    BackpackViewModel vm;

    public override void OnOpen(object data)
    {
        base.OnOpen(data);

        vm = data as BackpackViewModel ?? GameContext.Instance.BackpackVM;
        Bind(vm);
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

        topView.Bind(viewModel.topVM);
        middleView.Bind(viewModel.middleVM, viewModel.infoVM);

        if (bottomHub != null)
        {
            bottomHub.SetActive(true);
        }
    }

    void CloseSelf()
    {
        UIManager.Instance.Close(Handle.uiType);
    }
}
